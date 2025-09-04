' ReferenceManagerDialog.vb - Reference management dialog UI
Imports Gtk
Imports System.IO
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports System.Linq
Imports SimpleIDE.Managers

Namespace Dialogs
    
    Public Class ReferenceManagerDialog
        Inherits Dialog
        
        ' Private fields
        Private pNotebook As Notebook
        
        ' Public properties
        Public ReadOnly Property Notebook As Notebook
            Get
                Return pNotebook
            End Get
        End Property
        Private pProjectFile As String
        Private pNuGetClient As NuGetClient
        Private pSettingsManager As SettingsManager
        Private pProjectManager As ProjectManager
        
        ' Assembly tab components
        Private pAssemblyTreeView As TreeView
        Private pAssemblyListStore As ListStore
        Private pAssemblySearchEntry As SearchEntry
        Private pAssemblyAddButton As Button
        Private pAssemblyRemoveButton As Button
        Private pBrowseAssemblyButton As Button
        
        ' NuGet tab components
        Private pNuGetTreeView As TreeView
        Private pNuGetListStore As ListStore
        Private pNuGetSearchEntry As SearchEntry
        Private pNuGetSearchButton As Button
        Private pNuGetAddButton As Button
        Private pNuGetRemoveButton As Button
        Private pNuGetUpdateButton As Button
        Private pNuGetVersionCombo As ComboBoxText
        Private pNuGetSpinner As Spinner
        Private pNuGetStatusLabel As Label
        Private pCurrentSearchTask As Task(Of NuGetClient.SearchResult)
        
        ' Project tab components
        Private pProjectTreeView As TreeView
        Private pProjectListStore As ListStore
        Private pProjectAddButton As Button
        Private pProjectRemoveButton As Button
        Private pProjectBrowseButton As Button
        
        ' Current references
        Private pCurrentReferences As List(Of ReferenceManager.ReferenceInfo)
        
        ' Events
        Public Event ReferencesChanged()

        Private ReadOnly Property pReferenceManager() As ReferenceManager
            Get
                Return pProjectManager.ReferenceManager
            End Get
        End Property
        
        ''' <summary>
        ''' Creates a new ReferenceManagerDialog
        ''' </summary>
        ''' <param name="vParent">Parent window</param>
        ''' <param name="vProjectFile">Path to the project file</param>
        ''' <param name="vProjectManager">The ProjectManager instance</param>
        Public Sub New(vParent As Window, vProjectFile As String, vProjectManager As ProjectManager)
            MyBase.New("Reference Manager", vParent, DialogFlags.Modal)
            
            pProjectFile = vProjectFile
            pProjectManager = vProjectManager
            
            ' Remove this line - we don't need a separate ReferenceManager instance:
            ' pReferenceManager = New ReferenceManager()
            
            ' Set dialog properties
            SetDefaultSize(800, 600)
            
            ' Create UI
            BuildUI()
            
            ' Load current references through ProjectManager
            LoadCurrentReferences()
            
            ' Wire up events
            AddHandler Me.Response, AddressOf OnResponse
            
            ShowAll()
        End Sub
                
        Private Sub BuildUI()
            Try
                ' Create main vbox
                Dim lVBox As New Box(Orientation.Vertical, 5)
                
                ' Create notebook
                pNotebook = New Notebook()
                pNotebook.BorderWidth = 5
                
                ' Add tabs
                pNotebook.AppendPage(CreateAssembliesTab(), New Label("Assemblies"))
                pNotebook.AppendPage(CreateNuGetTab(), New Label("NuGet Packages"))
                pNotebook.AppendPage(CreateProjectsTab(), New Label("Projects"))
                
                lVBox.PackStart(pNotebook, True, True, 0)
                
                ' Add to content area
                ContentArea.Add(lVBox)
                
                ' Add dialog buttons
                AddButton("Close", ResponseType.Close)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"error building UI: {ex.Message}")
            End Try
        End Sub
        
        Private Function CreateAssembliesTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 5)
            lVBox.BorderWidth = 10
            
            ' Search box
            Dim lSearchBox As New Box(Orientation.Horizontal, 5)
            lSearchBox.PackStart(New Label("Filter:"), False, False, 0)
            
            pAssemblySearchEntry = New SearchEntry()
            pAssemblySearchEntry.PlaceholderText = "Type to filter assemblies..."
            AddHandler pAssemblySearchEntry.Changed, AddressOf OnAssemblySearchChanged
            lSearchBox.PackStart(pAssemblySearchEntry, True, True, 0)
            
            lVBox.PackStart(lSearchBox, False, False, 0)
            
            ' TreeView
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.ShadowType = ShadowType.in
            
            ' Create list store
            pAssemblyListStore = New ListStore(GetType(Boolean), GetType(String), GetType(String), GetType(String), GetType(String), GetType(Object))
            
            pAssemblyTreeView = New TreeView(pAssemblyListStore)
            pAssemblyTreeView.HeadersVisible = True
            pAssemblyTreeView.RubberBanding = True
            
            ' Columns
            ' Selected checkbox
            Dim lToggle As New CellRendererToggle()
            lToggle.Activatable = True
            AddHandler lToggle.Toggled, AddressOf OnAssemblyToggled
            Dim lSelectedCol As New TreeViewColumn("", lToggle, "active", 0)
            pAssemblyTreeView.AppendColumn(lSelectedCol)
            
            ' Name
            pAssemblyTreeView.AppendColumn("Name", New CellRendererText(), "text", 1)
            
            ' Version
            pAssemblyTreeView.AppendColumn("Version", New CellRendererText(), "text", 2)
            
            ' Runtime
            pAssemblyTreeView.AppendColumn("Runtime", New CellRendererText(), "text", 3)
            
            ' Path
            Dim lPathRenderer As New CellRendererText()
            lPathRenderer.Ellipsize = Pango.EllipsizeMode.Middle
            pAssemblyTreeView.AppendColumn("Path", lPathRenderer, "text", 4)
            
            lScrolled.Add(pAssemblyTreeView)
            lVBox.PackStart(lScrolled, True, True, 0)
            
            ' Button box
            Dim lButtonBox As New Box(Orientation.Horizontal, 5)
            
            pBrowseAssemblyButton = New Button("Browse...")
            AddHandler pBrowseAssemblyButton.Clicked, AddressOf OnBrowseAssembly
            lButtonBox.PackStart(pBrowseAssemblyButton, False, False, 0)
            
            lButtonBox.PackStart(New Label(""), True, True, 0) ' Spacer
            
            pAssemblyAddButton = New Button("Add Selected")
            pAssemblyAddButton.Sensitive = False
            AddHandler pAssemblyAddButton.Clicked, AddressOf OnAddAssemblies
            lButtonBox.PackStart(pAssemblyAddButton, False, False, 0)
            
            pAssemblyRemoveButton = New Button("Remove Selected")
            pAssemblyRemoveButton.Sensitive = False
            AddHandler pAssemblyRemoveButton.Clicked, AddressOf OnRemoveAssemblies
            lButtonBox.PackStart(pAssemblyRemoveButton, False, False, 0)
            
            lVBox.PackStart(lButtonBox, False, False, 0)
            
            ' Load runtime assemblies
            LoadRuntimeAssemblies()
            
            Return lVBox
        End Function
        
        Private Function CreateNuGetTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 5)
            lVBox.BorderWidth = 10
            
            ' Search box
            Dim lSearchBox As New Box(Orientation.Horizontal, 5)
            lSearchBox.PackStart(New Label("Search:"), False, False, 0)
            
            pNuGetSearchEntry = New SearchEntry()
            pNuGetSearchEntry.PlaceholderText = "Search NuGet Packages..."
            AddHandler pNuGetSearchEntry.Activated, AddressOf OnNuGetSearch
            lSearchBox.PackStart(pNuGetSearchEntry, True, True, 0)
            
            pNuGetSearchButton = New Button("Search")
            AddHandler pNuGetSearchButton.Clicked, AddressOf OnNuGetSearch
            lSearchBox.PackStart(pNuGetSearchButton, False, False, 0)
            
            lVBox.PackStart(lSearchBox, False, False, 0)
            
            ' TreeView
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.ShadowType = ShadowType.in
            
            ' Create list store
            pNuGetListStore = New ListStore(GetType(String), GetType(String), GetType(String), GetType(Long), GetType(Boolean), GetType(String), GetType(Object))
            
            pNuGetTreeView = New TreeView(pNuGetListStore)
            pNuGetTreeView.HeadersVisible = True
            
            ' Columns
            pNuGetTreeView.AppendColumn("Package", New CellRendererText(), "text", 0)
            pNuGetTreeView.AppendColumn("Version", New CellRendererText(), "text", 1)
            
            ' Description with wrapping
            Dim lDescRenderer As New CellRendererText()
            lDescRenderer.WrapMode = Pango.WrapMode.Word
            lDescRenderer.WrapWidth = 300
            pNuGetTreeView.AppendColumn("Description", lDescRenderer, "text", 2)
            
            pNuGetTreeView.AppendColumn("Downloads", New CellRendererText(), "text", 3)
            
            ' Installed indicator
            Dim lInstalledRenderer As New CellRendererText()
            lInstalledRenderer.Weight = 700 ' Bold
            pNuGetTreeView.AppendColumn("Installed", lInstalledRenderer, "Text", 5)
            
            ' Selection handler
            AddHandler pNuGetTreeView.Selection.Changed, AddressOf OnNuGetSelectionChanged
            
            lScrolled.Add(pNuGetTreeView)
            lVBox.PackStart(lScrolled, True, True, 0)
            
            ' Version selection box
            Dim lVersionBox As New Box(Orientation.Horizontal, 5)
            lVersionBox.PackStart(New Label("Version:"), False, False, 0)
            
            pNuGetVersionCombo = New ComboBoxText()
            pNuGetVersionCombo.Sensitive = False
            lVersionBox.PackStart(pNuGetVersionCombo, False, False, 0)
            
            lVBox.PackStart(lVersionBox, False, False, 0)
            
            ' Status box
            Dim lStatusBox As New Box(Orientation.Horizontal, 5)
            
            pNuGetSpinner = New Spinner()
            lStatusBox.PackStart(pNuGetSpinner, False, False, 0)
            
            pNuGetStatusLabel = New Label("Ready")
            lStatusBox.PackStart(pNuGetStatusLabel, False, False, 0)
            
            lVBox.PackStart(lStatusBox, False, False, 0)
            
            ' Button box
            Dim lButtonBox As New Box(Orientation.Horizontal, 5)
            
            lButtonBox.PackStart(New Label(""), True, True, 0) ' Spacer
            
            pNuGetAddButton = New Button("Install")
            pNuGetAddButton.Sensitive = False
            AddHandler pNuGetAddButton.Clicked, AddressOf OnInstallPackage
            lButtonBox.PackStart(pNuGetAddButton, False, False, 0)
            
            pNuGetUpdateButton = New Button("Update")
            pNuGetUpdateButton.Sensitive = False
            AddHandler pNuGetUpdateButton.Clicked, AddressOf OnUpdatePackage
            lButtonBox.PackStart(pNuGetUpdateButton, False, False, 0)
            
            pNuGetRemoveButton = New Button("Uninstall")
            pNuGetRemoveButton.Sensitive = False
            AddHandler pNuGetRemoveButton.Clicked, AddressOf OnUninstallPackage
            lButtonBox.PackStart(pNuGetRemoveButton, False, False, 0)
            
            lVBox.PackStart(lButtonBox, False, False, 0)
            
            ' Load installed packages
            LoadInstalledPackages()
            
            Return lVBox
        End Function
        
        Private Function CreateProjectsTab() As Widget
            Dim lVBox As New Box(Orientation.Vertical, 5)
            lVBox.BorderWidth = 10
            
            ' Info label
            Dim lInfoLabel As New Label("Add References to other projects in your solution")
            lInfoLabel.Halign = Align.Start
            lVBox.PackStart(lInfoLabel, False, False, 0)
            
            ' TreeView
            Dim lScrolled As New ScrolledWindow()
            lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            lScrolled.ShadowType = ShadowType.in
            
            ' Create list store
            pProjectListStore = New ListStore(GetType(String), GetType(String), GetType(Boolean))
            
            pProjectTreeView = New TreeView(pProjectListStore)
            pProjectTreeView.HeadersVisible = True
            
            ' Columns
            pProjectTreeView.AppendColumn("Project", New CellRendererText(), "text", 0)
            pProjectTreeView.AppendColumn("Path", New CellRendererText(), "text", 1)
            
            ' Selection handler
            AddHandler pProjectTreeView.Selection.Changed, AddressOf OnProjectSelectionChanged
            
            lScrolled.Add(pProjectTreeView)
            lVBox.PackStart(lScrolled, True, True, 0)
            
            ' Button box
            Dim lButtonBox As New Box(Orientation.Horizontal, 5)
            
            pProjectBrowseButton = New Button("Browse...")
            AddHandler pProjectBrowseButton.Clicked, AddressOf OnBrowseProject
            lButtonBox.PackStart(pProjectBrowseButton, False, False, 0)
            
            lButtonBox.PackStart(New Label(""), True, True, 0) ' Spacer
            
            pProjectAddButton = New Button("Add Reference")
            pProjectAddButton.Sensitive = False
            AddHandler pProjectAddButton.Clicked, AddressOf OnAddProject
            lButtonBox.PackStart(pProjectAddButton, False, False, 0)
            
            pProjectRemoveButton = New Button("Remove")
            pProjectRemoveButton.Sensitive = False
            AddHandler pProjectRemoveButton.Clicked, AddressOf OnRemoveProject
            lButtonBox.PackStart(pProjectRemoveButton, False, False, 0)
            
            lVBox.PackStart(lButtonBox, False, False, 0)
            
            ' Load current project references
            LoadProjectReferences()
            
            Return lVBox
        End Function
        
        ''' <summary>
        ''' Load current references from project through ProjectManager
        ''' </summary>
        Private Sub LoadCurrentReferences()
            Try
                If pProjectManager IsNot Nothing Then
                    ' Get references through ProjectManager
                    pCurrentReferences = pProjectManager.ProjectReferences
                    
                    ' If not loaded yet, load them
                    If pCurrentReferences Is Nothing OrElse pCurrentReferences.Count = 0 Then
                        pProjectManager.LoadProjectReferences()
                        pCurrentReferences = pProjectManager.ProjectReferences
                    End If
                Else
                    pCurrentReferences = New List(Of ReferenceManager.ReferenceInfo)()
                End If
                
                ' Update UI to show current references
                UpdateAssemblyList()
                UpdateProjectList()
                
            Catch ex As Exception
                Console.WriteLine($"Error loading References: {ex.Message}")
            End Try
        End Sub
                
        ' Load runtime assemblies
        Private Sub LoadRuntimeAssemblies()
            Try
                pAssemblyListStore.Clear()
                
                ' Get runtime assemblies
                Dim lAssemblies As List(Of AssemblyBrowser.AssemblyInfo) = AssemblyBrowser.GetRuntimeAssemblies()
                
                for each lAssembly in lAssemblies
                    ' Check if already referenced
                    Dim lIsReferenced As Boolean = pCurrentReferences.any(Function(r) r.Type = ReferenceManager.ReferenceType.eAssembly AndAlso r.Name = lAssembly.Name)
                    
                    Dim lIter As TreeIter = pAssemblyListStore.AppendValues(
                        lIsReferenced,
                        lAssembly.Name,
                        lAssembly.Version,
                        lAssembly.Runtime,
                        lAssembly.Location,
                        lAssembly
                    )
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error loading Runtime assemblies: {ex.Message}")
            End Try
        End Sub
        
        ' Assembly search changed
        Private Sub OnAssemblySearchChanged(vSender As Object, vE As EventArgs)
            Try
                Dim lFilter As String = pAssemblySearchEntry.Text.ToLower()
                
                ' TODO: Implement filtering
                ' For now, just update button state
                UpdateAssemblyButtons()
                
            Catch ex As Exception
                Console.WriteLine($"error filtering assemblies: {ex.Message}")
            End Try
        End Sub
        
        ' Assembly toggled
        Private Sub OnAssemblyToggled(vSender As Object, vE As ToggledArgs)
            Try
                Dim lPath As New TreePath(vE.Path)
                Dim lIter As TreeIter
                
                If pAssemblyListStore.GetIter(lIter, lPath) Then
                    Dim lCurrentValue As Boolean = CBool(pAssemblyListStore.GetValue(lIter, 0))
                    pAssemblyListStore.SetValue(lIter, 0, Not lCurrentValue)
                    
                    UpdateAssemblyButtons()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error toggling assembly: {ex.Message}")
            End Try
        End Sub
        
        ' Update assembly buttons
        Private Sub UpdateAssemblyButtons()
            Try
                Dim lHasSelected As Boolean = False
                Dim lHasUnselected As Boolean = False
                
                Dim lIter As TreeIter
                If pAssemblyListStore.GetIterFirst(lIter) Then
                    Do
                        Dim lSelected As Boolean = CBool(pAssemblyListStore.GetValue(lIter, 0))
                        If lSelected Then
                            lHasSelected = True
                        Else
                            lHasUnselected = True
                        End If
                    Loop While pAssemblyListStore.IterNext(lIter)
                End If
                
                pAssemblyAddButton.Sensitive = lHasUnselected
                pAssemblyRemoveButton.Sensitive = lHasSelected
                
            Catch ex As Exception
                Console.WriteLine($"error updating assembly buttons: {ex.Message}")
            End Try
        End Sub
        
        ' Browse for assembly
        Private Sub OnBrowseAssembly(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select Assembly",
                    Me,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept
                )
                
                ' Add filters
                Dim lFilter As New FileFilter()
                lFilter.Name = "Assembly Files (*.dll)"
                lFilter.AddPattern("*.dll")
                lDialog.AddFilter(lFilter)
                
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    ' Add the selected assembly
                    Dim lAssemblyPath As String = lDialog.FileName
                    
                    ' TODO: Add to list and mark as selected
                    
                    ' Add to recent
                    AssemblyBrowser.AddToRecentAssemblies(pSettingsManager, lAssemblyPath)
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error browsing for assembly: {ex.Message}")
            End Try
        End Sub
        
        ' Add selected assemblies
        Private Sub OnAddAssemblies(vSender As Object, vE As EventArgs)
            Try
                If pProjectManager Is Nothing Then
                    ShowError("No project manager available")
                    Return
                End If
                
                Dim lAddedCount As Integer = 0
                Dim lIter As TreeIter
                
                If pAssemblyListStore.GetIterFirst(lIter) Then
                    Do
                        Dim lSelected As Boolean = CBool(pAssemblyListStore.GetValue(lIter, 0))
                        If lSelected Then
                            Dim lAssembly As AssemblyBrowser.AssemblyInfo = CType(pAssemblyListStore.GetValue(lIter, 5), AssemblyBrowser.AssemblyInfo)
                            
                            ' Check if not already referenced
                            If Not pProjectManager.HasReference(lAssembly.Name, ReferenceManager.ReferenceType.eAssembly) Then
                                ' Add reference through ProjectManager
                                If pProjectManager.AddAssemblyReference(lAssembly.Name, lAssembly.Location) Then
                                    lAddedCount += 1
                                End If
                            End If
                        End If
                    Loop While pAssemblyListStore.IterNext(lIter)
                End If
                
                If lAddedCount > 0 Then
                    ShowInfo($"Added {lAddedCount} assembly Reference(s)")
                    RaiseEvent ReferencesChanged()
                    LoadCurrentReferences()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Error adding assemblies: {ex.Message}")
                ShowError($"Error adding assemblies: {ex.Message}")
            End Try
        End Sub
        
        ' Remove selected assemblies
        Private Sub OnRemoveAssemblies(vSender As Object, vE As EventArgs)
            Try
                If pProjectManager Is Nothing Then
                    ShowError("No project manager available")
                    Return
                End If
                
                Dim lIter As TreeIter
                Dim lRemovedCount As Integer = 0
                
                If pAssemblyListStore.GetIterFirst(lIter) Then
                    Do
                        Dim lSelected As Boolean = CBool(pAssemblyListStore.GetValue(lIter, 0))
                        If lSelected Then
                            Dim lAssembly As AssemblyBrowser.AssemblyInfo = CType(pAssemblyListStore.GetValue(lIter, 5), AssemblyBrowser.AssemblyInfo)
                            
                            ' Remove reference through ProjectManager
                            If pProjectManager.RemoveReference(lAssembly.Name, ReferenceManager.ReferenceType.eAssembly) Then
                                lRemovedCount += 1
                            End If
                        End If
                    Loop While pAssemblyListStore.IterNext(lIter)
                End If
                
                If lRemovedCount > 0 Then
                    ShowInfo($"Removed {lRemovedCount} assembly Reference(s)")
                    RaiseEvent ReferencesChanged()
                    LoadCurrentReferences()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Error removing assemblies: {ex.Message}")
                ShowError($"Error removing assemblies: {ex.Message}")
            End Try
        End Sub
                
        ' NuGet search
        Private Sub OnNuGetSearch(vSender As Object, vE As EventArgs)
            Try
                Dim lQuery As String = pNuGetSearchEntry.Text.Trim()
                If String.IsNullOrEmpty(lQuery) Then Return
                
                ' Cancel previous search if running
                If pCurrentSearchTask IsNot Nothing AndAlso Not pCurrentSearchTask.IsCompleted Then
                    ' TODO: Implement cancellation
                End If
                
                ' Start search
                pNuGetSpinner.Start()
                pNuGetStatusLabel.Text = "Searching..."
                pNuGetSearchButton.Sensitive = False
                
                ' Clear current results
                pNuGetListStore.Clear()
                
                ' Start async search
                pCurrentSearchTask = Task.Run(Async Function() Await pNuGetClient.SearchPackagesAsync(lQuery, 0, 50))
                pCurrentSearchTask.ContinueWith(Sub(t) GLib.Idle.Add(Function() OnNuGetSearchComplete(t)))
                
            Catch ex As Exception
                Console.WriteLine($"error searching NuGet: {ex.Message}")
                ShowError($"error searching NuGet: {ex.Message}")
            End Try
        End Sub
        
        ' NuGet search complete
        Private Function OnNuGetSearchComplete(vTask As Task(Of NuGetClient.SearchResult)) As Boolean
            Try
                pNuGetSpinner.Stop()
                pNuGetSearchButton.Sensitive = True
                
                If vTask.IsFaulted Then
                    pNuGetStatusLabel.Text = "Search failed"
                    ShowError($"Search failed: {vTask.Exception.GetBaseException().Message}")
                    Return False
                End If
                
                Dim lResult As NuGetClient.SearchResult = vTask.Result
                pNuGetStatusLabel.Text = $"Found {lResult.TotalHits} Packages"
                
                ' Populate results
                for each lPackage in lResult.Packages
                    ' Check if installed
                    Dim lInstalledVersion As String = ""
                    lPackage.IsInstalled = pNuGetClient.IsPackageInstalled(pProjectFile, lPackage.Id, lInstalledVersion)
                    lPackage.InstalledVersion = lInstalledVersion
                    
                    Dim lIter As TreeIter = pNuGetListStore.AppendValues(
                        lPackage.Id,
                        lPackage.Version,
                        lPackage.Description,
                        lPackage.TotalDownloads,
                        lPackage.IsInstalled,
                        If(lPackage.IsInstalled, lInstalledVersion, ""),
                        lPackage
                    )
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error completing NuGet search: {ex.Message}")
            End Try
            
            Return False
        End Function
        
        ' NuGet selection changed
        Private Sub OnNuGetSelectionChanged(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pNuGetTreeView.Selection
                Dim lIter As TreeIter
                
                If lSelection.GetSelected(lIter) Then
                    Dim lPackage As NuGetClient.PackageInfo = CType(pNuGetListStore.GetValue(lIter, 6), NuGetClient.PackageInfo)
                    
                    ' Update buttons
                    pNuGetAddButton.Sensitive = Not lPackage.IsInstalled
                    pNuGetRemoveButton.Sensitive = lPackage.IsInstalled
                    pNuGetUpdateButton.Sensitive = False ' Will check for updates
                    
                    ' Clear version combo
                    pNuGetVersionCombo.RemoveAll()
                    pNuGetVersionCombo.Sensitive = False
                    
                    ' Load versions async
                    Task.Run(Async Function() Await pNuGetClient.GetPackageVersionsAsync(lPackage.Id)).ContinueWith(
                        Sub(t) GLib.Idle.Add(Function() OnPackageVersionsLoaded(t, lPackage))
                    )
                Else
                    ' No selection
                    pNuGetAddButton.Sensitive = False
                    pNuGetRemoveButton.Sensitive = False
                    pNuGetUpdateButton.Sensitive = False
                    pNuGetVersionCombo.Sensitive = False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error handling NuGet selection: {ex.Message}")
            End Try
        End Sub
        
        ' Package versions loaded
        Private Function OnPackageVersionsLoaded(vTask As Task(Of List(Of String)), vPackage As NuGetClient.PackageInfo) As Boolean
            Try
                If vTask.IsCompletedSuccessfully Then
                    Dim lVersions As List(Of String) = vTask.Result
                    
                    ' Populate version combo
                    for each lVersion in lVersions
                        pNuGetVersionCombo.AppendText(lVersion)
                    Next
                    
                    ' Select current version
                    If lVersions.Contains(vPackage.Version) Then
                        pNuGetVersionCombo.Active = lVersions.IndexOf(vPackage.Version)
                    ElseIf lVersions.Count > 0 Then
                        pNuGetVersionCombo.Active = 0 ' Latest
                    End If
                    
                    pNuGetVersionCombo.Sensitive = True
                    
                    ' Check if update available
                    If vPackage.IsInstalled AndAlso lVersions.Count > 0 Then
                        Dim lLatestVersion As String = lVersions(0)
                        If lLatestVersion <> vPackage.InstalledVersion Then
                            pNuGetUpdateButton.Sensitive = True
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error loading Package Versions: {ex.Message}")
            End Try
            
            Return False
        End Function
        
        ' Install package
        Private Sub OnInstallPackage(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pNuGetTreeView.Selection
                Dim lIter As TreeIter
                
                If lSelection.GetSelected(lIter) Then
                    Dim lPackage As NuGetClient.PackageInfo = CType(pNuGetListStore.GetValue(lIter, 6), NuGetClient.PackageInfo)
                    Dim lVersion As String = pNuGetVersionCombo.ActiveText
                    
                    If String.IsNullOrEmpty(lVersion) Then
                        lVersion = lPackage.Version
                    End If
                    
                    ' Add package reference
                    If pReferenceManager.AddPackageReference(pProjectFile, lPackage.Id, lVersion) Then
                        ShowInfo($"Installed {lPackage.Id} {lVersion}")
                        RaiseEvent ReferencesChanged()
                        
                        ' Update UI
                        lPackage.IsInstalled = True
                        lPackage.InstalledVersion = lVersion
                        pNuGetListStore.SetValue(lIter, 4, True)
                        pNuGetListStore.SetValue(lIter, 5, lVersion)
                        
                        ' Update buttons
                        pNuGetAddButton.Sensitive = False
                        pNuGetRemoveButton.Sensitive = True
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error installing Package: {ex.Message}")
                ShowError($"error installing Package: {ex.Message}")
            End Try
        End Sub
        
        ' Update package
        Private Sub OnUpdatePackage(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pNuGetTreeView.Selection
                Dim lIter As TreeIter
                
                If lSelection.GetSelected(lIter) Then
                    Dim lPackage As NuGetClient.PackageInfo = CType(pNuGetListStore.GetValue(lIter, 6), NuGetClient.PackageInfo)
                    Dim lVersion As String = pNuGetVersionCombo.ActiveText
                    
                    If String.IsNullOrEmpty(lVersion) Then Return
                    
                    ' Update package reference
                    If pReferenceManager.UpdatePackageReference(pProjectFile, lPackage.Id, lVersion) Then
                        ShowInfo($"updated {lPackage.Id} to {lVersion}")
                        RaiseEvent ReferencesChanged()
                        
                        ' Update UI
                        lPackage.InstalledVersion = lVersion
                        pNuGetListStore.SetValue(lIter, 5, lVersion)
                        pNuGetUpdateButton.Sensitive = False
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error updating Package: {ex.Message}")
                ShowError($"error updating Package: {ex.Message}")
            End Try
        End Sub
        
        ' Uninstall package
        Private Sub OnUninstallPackage(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pNuGetTreeView.Selection
                Dim lIter As TreeIter
                
                If lSelection.GetSelected(lIter) Then
                    Dim lPackage As NuGetClient.PackageInfo = CType(pNuGetListStore.GetValue(lIter, 6), NuGetClient.PackageInfo)
                    
                    ' Remove package reference
                    If pReferenceManager.RemoveReference(pProjectFile, lPackage.Id, ReferenceManager.ReferenceType.ePackage) Then
                        ShowInfo($"Uninstalled {lPackage.Id}")
                        RaiseEvent ReferencesChanged()
                        
                        ' Update UI
                        lPackage.IsInstalled = False
                        lPackage.InstalledVersion = ""
                        pNuGetListStore.SetValue(lIter, 4, False)
                        pNuGetListStore.SetValue(lIter, 5, "")
                        
                        ' Update buttons
                        pNuGetAddButton.Sensitive = True
                        pNuGetRemoveButton.Sensitive = False
                        pNuGetUpdateButton.Sensitive = False
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error uninstalling Package: {ex.Message}")
                ShowError($"error uninstalling Package: {ex.Message}")
            End Try
        End Sub
        
        ' Load installed packages
        Private Sub LoadInstalledPackages()
            Try
                pNuGetListStore.Clear()
                
                ' Get package references
                Dim lPackageRefs As List(Of ReferenceManager.ReferenceInfo) = pCurrentReferences.Where(
                    Function(r) r.Type = ReferenceManager.ReferenceType.ePackage
                ).ToList()
                
                for each lRef in lPackageRefs
                    Dim lPackage As New NuGetClient.PackageInfo()
                    lPackage.Id = lRef.Name
                    lPackage.Version = lRef.Version
                    lPackage.InstalledVersion = lRef.Version
                    lPackage.IsInstalled = True
                    lPackage.Description = "Installed Package"
                    
                    Dim lIter As TreeIter = pNuGetListStore.AppendValues(
                        lPackage.Id,
                        lPackage.Version,
                        lPackage.Description,
                        0L,
                        True,
                        lPackage.InstalledVersion,
                        lPackage
                    )
                Next
                
                pNuGetStatusLabel.Text = $"{lPackageRefs.Count} installed Packages"
                
            Catch ex As Exception
                Console.WriteLine($"error loading installed Packages: {ex.Message}")
            End Try
        End Sub
        
        ' Load project references
        Private Sub LoadProjectReferences()
            Try
                pProjectListStore.Clear()
                
                ' Get project references
                Dim lProjectRefs As List(Of ReferenceManager.ReferenceInfo) = pCurrentReferences.Where(
                    Function(r) r.Type = ReferenceManager.ReferenceType.eProject
                ).ToList()
                
                for each lRef in lProjectRefs
                    Dim lIter As TreeIter = pProjectListStore.AppendValues(
                        lRef.Name,
                        lRef.Path,
                        True ' Is Reference
                    )
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error loading project References: {ex.Message}")
            End Try
        End Sub
        
        ' Update assembly list
        Private Sub UpdateAssemblyList()
            Try
                ' Update checkboxes for referenced assemblies
                Dim lIter As TreeIter
                If pAssemblyListStore.GetIterFirst(lIter) Then
                    Do
                        Dim lAssembly As AssemblyBrowser.AssemblyInfo = CType(pAssemblyListStore.GetValue(lIter, 5), AssemblyBrowser.AssemblyInfo)
                        Dim lIsReferenced As Boolean = pCurrentReferences.any(
                            Function(r) r.Type = ReferenceManager.ReferenceType.eAssembly AndAlso r.Name = lAssembly.Name
                        )
                        pAssemblyListStore.SetValue(lIter, 0, lIsReferenced)
                    Loop While pAssemblyListStore.IterNext(lIter)
                End If
                
                UpdateAssemblyButtons()
                
            Catch ex As Exception
                Console.WriteLine($"error updating assembly list: {ex.Message}")
            End Try
        End Sub
        
        ' Update project list
        Private Sub UpdateProjectList()
            LoadProjectReferences()
        End Sub
        
        ' Project selection changed
        Private Sub OnProjectSelectionChanged(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pProjectTreeView.Selection
                Dim lIter As TreeIter
                
                If lSelection.GetSelected(lIter) Then
                    Dim lIsReference As Boolean = CBool(pProjectListStore.GetValue(lIter, 2))
                    
                    pProjectAddButton.Sensitive = Not lIsReference
                    pProjectRemoveButton.Sensitive = lIsReference
                Else
                    pProjectAddButton.Sensitive = False
                    pProjectRemoveButton.Sensitive = False
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error handling project selection: {ex.Message}")
            End Try
        End Sub
        
        ' Browse for project
        Private Sub OnBrowseProject(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select project",
                    Me,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept
                )
                
                ' Add filters
                Dim lFilter As New FileFilter()
                lFilter.Name = "project Files"
                lFilter.AddPattern("*.vbproj")
                lFilter.AddPattern("*.csproj")
                lFilter.AddPattern("*.fsproj")
                lDialog.AddFilter(lFilter)
                
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)
                
                ' Set initial directory
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                lDialog.SetCurrentFolder(lProjectDir)
                
                If lDialog.Run() = CInt(ResponseType.Accept) Then
                    Dim lSelectedProject As String = lDialog.FileName
                    
                    ' Validate
                    Dim lValidation As ReferenceManager.ValidationResult = pReferenceManager.ValidateProjectReference(pProjectFile, lSelectedProject)
                    
                    If lValidation.IsValid Then
                        ' Add to list
                        Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(lSelectedProject)
                        Dim lRelativePath As String = GetRelativePath(lProjectDir, lSelectedProject)
                        
                        Dim lIter As TreeIter = pProjectListStore.AppendValues(
                            lProjectName,
                            lRelativePath,
                            False ' Not yet a Reference
                        )
                        
                        ' Select it
                        pProjectTreeView.Selection.SelectIter(lIter)
                    Else
                        ShowError(lValidation.ErrorMessage)
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error browsing for project: {ex.Message}")
                ShowError($"error browsing for project: {ex.Message}")
            End Try
        End Sub
        
        ' Add project reference
        Private Sub OnAddProject(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pProjectTreeView.Selection
                Dim lIter As TreeIter
                
                If lSelection.GetSelected(lIter) Then
                    Dim lProjectPath As String = CStr(pProjectListStore.GetValue(lIter, 1))
                    
                    ' Make absolute path if relative
                    If Not System.IO.Path.IsPathRooted(lProjectPath) Then
                        Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                        lProjectPath = System.IO.Path.Combine(lProjectDir, lProjectPath)
                    End If
                    
                    ' Add reference
                    If pReferenceManager.AddProjectReference(pProjectFile, lProjectPath) Then
                        ShowInfo("Added project Reference")
                        RaiseEvent ReferencesChanged()
                        LoadCurrentReferences()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error adding project Reference: {ex.Message}")
                ShowError($"error adding project Reference: {ex.Message}")
            End Try
        End Sub
        
        ' Remove project reference
        Private Sub OnRemoveProject(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = pProjectTreeView.Selection
                Dim lIter As TreeIter
                
                If lSelection.GetSelected(lIter) Then
                    Dim lProjectName As String = CStr(pProjectListStore.GetValue(lIter, 0))
                    
                    ' Remove reference
                    If pReferenceManager.RemoveReference(pProjectFile, lProjectName, ReferenceManager.ReferenceType.eProject) Then
                        ShowInfo("Removed project Reference")
                        RaiseEvent ReferencesChanged()
                        LoadCurrentReferences()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error removing project Reference: {ex.Message}")
                ShowError($"error removing project Reference: {ex.Message}")
            End Try
        End Sub
        
        ' Get relative path
        Private Function GetRelativePath(vFrom As String, vTo As String) As String
            Try
                Dim lFromUri As New Uri(vFrom & System.IO.Path.DirectorySeparatorChar)
                Dim lToUri As New Uri(vTo)
                
                Dim lRelativeUri As Uri = lFromUri.MakeRelativeUri(lToUri)
                Dim lRelativePath As String = Uri.UnescapeDataString(lRelativeUri.ToString())
                
                Return lRelativePath.Replace("/"c, System.IO.Path.DirectorySeparatorChar)
                
            Catch ex As Exception
                Return vTo
            End Try
        End Function
        
        ' Dialog response
        Private Shadows Sub OnResponse(vSender As Object, vE As ResponseArgs)
            Select Case vE.ResponseId
                Case ResponseType.Close
                    Me.Hide()
            End Select
        End Sub
        
        ' Show info message
        Private Sub ShowInfo(vMessage As String)
            Dim lDialog As New MessageDialog(
                Me,
                DialogFlags.Modal,
                MessageType.Info,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Run()
            lDialog.Destroy()
        End Sub
        
        ' Show error message
        Private Sub ShowError(vMessage As String)
            Dim lDialog As New MessageDialog(
                Me,
                DialogFlags.Modal,
                MessageType.error,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Run()
            lDialog.Destroy()
        End Sub
    End Class

End Namespace
