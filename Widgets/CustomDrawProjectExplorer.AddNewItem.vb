' Widgets/CustomDrawProjectExplorer.AddNewItem.vb - Add new item functionality
' Created: 2025-08-21
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Utilities

' CustomDrawProjectExplorer.AddNewItem.vb
' Created: 2025-08-21 05:17:30

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing Add New Item functionality
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        
        ' ===== Public Methods =====
        
        
        ' ===== Add New Item Handlers (Updated Implementations) =====
        
        ''' <summary>
        ''' Handles adding a new class to the project
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Event arguments</param>
        Private Sub OnContextMenuAddClass(vSender As Object, vArgs As EventArgs)
            Try
                ShowAddNewItemDialog("Class", ".vb", AddressOf GenerateClassCode)
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddClass error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles adding a new module to the project
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Event arguments</param>
        Private Sub OnContextMenuAddModule(vSender As Object, vArgs As EventArgs)
            Try
                ShowAddNewItemDialog("Module", ".vb", AddressOf GenerateModuleCode)
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddModule error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles adding a new interface to the project
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Event arguments</param>
        Private Sub OnContextMenuAddInterface(vSender As Object, vArgs As EventArgs)
            Try
                ShowAddNewItemDialog("Interface", ".vb", AddressOf GenerateInterfaceCode)
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddInterface error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles adding a new form to the project
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Event arguments</param>
        Private Sub OnContextMenuAddForm(vSender As Object, vArgs As EventArgs)
            Try
                ShowAddNewItemDialog("Windows Form", ".vb", AddressOf GenerateFormCode)
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddForm error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles adding a new user control to the project
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Event arguments</param>
        Private Sub OnContextMenuAddUserControl(vSender As Object, vArgs As EventArgs)
            Try
                ShowAddNewItemDialog("User Control", ".vb", AddressOf GenerateUserControlCode)
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddUserControl error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles adding a new folder to the project
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Event arguments</param>
        Private Sub OnContextMenuAddNewFolder(vSender As Object, vArgs As EventArgs)
            Try
                ShowAddNewFolderDialog()
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddNewFolder error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles adding an existing item to the project
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Event arguments</param>
        Private Sub OnContextMenuAddExistingItem(vSender As Object, vArgs As EventArgs)
            Try
                ShowAddExistingItemDialog()
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddExistingItem error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the generic Add New Item menu
        ''' </summary>
        ''' <param name="vSender">Event sender</param>
        ''' <param name="vArgs">Event arguments</param>
        Private Sub OnContextMenuAddNewItem(vSender As Object, vArgs As EventArgs)
            Try
                ShowAddNewItemSelector()
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuAddNewItem error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Dialog Methods =====
        
        ''' <summary>
        ''' Shows dialog for adding a new item of specified type
        ''' </summary>
        ''' <param name="vItemType">Type of item to add</param>
        ''' <param name="vExtension">File extension</param>
        ''' <param name="vCodeGenerator">Function to generate code content</param>
        Private Sub ShowAddNewItemDialog(vItemType As String, vExtension As String, vCodeGenerator As Func(Of String, String, String))
            Try
                ' Create dialog
                Dim lDialog As New Dialog($"Add New {vItemType}", 
                                        GetTopLevelWindow(), 
                                        DialogFlags.Modal Or DialogFlags.DestroyWithParent)
                
                lDialog.SetDefaultSize(400, 150)
                
                ' Create content area
                Dim lVBox As New Box(Orientation.Vertical, 8)
                lVBox.BorderWidth = 10
                
                ' Name label and entry
                Dim lNameLabel As New Label($"{vItemType} Name:")
                lNameLabel.Halign = Align.Start
                lVBox.PackStart(lNameLabel, False, False, 0)
                
                Dim lNameEntry As New Entry()
                lNameEntry.PlaceholderText = $"Enter {vItemType.ToLower()} name"
                ' Enable Enter key to activate default button
                lNameEntry.ActivatesDefault = True
                lVBox.PackStart(lNameEntry, False, False, 0)
                
                ' Location label
                Dim lLocationLabel As New Label("Location:")
                lLocationLabel.Halign = Align.Start
                lVBox.PackStart(lLocationLabel, False, False, 0)
                
                ' Get selected folder path
                Dim lSelectedPath As String = GetSelectedFolderPath()
                Dim lLocationEntry As New Entry()
                lLocationEntry.Text = lSelectedPath
                lLocationEntry.Sensitive = False
                lVBox.PackStart(lLocationEntry, False, False, 0)
                
                ' Add to content area
                lDialog.ContentArea.Add(lVBox)
                
                ' Add buttons
                lDialog.AddButton("Cancel", ResponseType.Cancel)
                Dim lAddButton As Widget = lDialog.AddButton("Add", ResponseType.Ok)
                
                ' Set default button
                lDialog.DefaultResponse = ResponseType.Ok
                lAddButton.CanDefault = True
                lAddButton.GrabDefault()
                
                ' Focus name entry
                lNameEntry.GrabFocus()
                
                ' Show all
                lDialog.ShowAll()
                
                ' Keep dialog open until valid input or cancel
                Dim lDone As Boolean = False
                While Not lDone
                    Dim lResponse As ResponseType = CType(lDialog.Run(), ResponseType)
                    
                    If lResponse = ResponseType.Ok Then
                        Dim lName As String = lNameEntry.Text.Trim()
                        
                        ' Check for empty name
                        If String.IsNullOrEmpty(lName) Then
                            ShowErrorDialog($"Please enter a {vItemType.ToLower()} name.")
                            ' Re-focus the entry and select all text for easy correction
                            lNameEntry.GrabFocus()
                            lNameEntry.SelectRegion(0, lNameEntry.Text.Length)
                            Continue While
                        End If
                        
                        ' Validate name characters
                        If Not IsValidItemName(lName) Then
                            ShowErrorDialog($"Invalid {vItemType.ToLower()} name. Use only letters, numbers, underscores, and dots (for namespaces).")
                            ' Re-focus and select for correction
                            lNameEntry.GrabFocus()
                            lNameEntry.SelectRegion(0, lNameEntry.Text.Length)
                            Continue While
                        End If
                        
                        ' Check if file already exists
                        Dim lFileName As String
                        If lName.Contains("."c) Then
                            ' If name contains dots (namespace), extract just the class name for the file
                            Dim lParts As String() = lName.Split("."c)
                            lFileName = lParts(lParts.Length - 1) & vExtension
                        Else
                            lFileName = lName & vExtension
                        End If
                        
                        Dim lFullPath As String = System.IO.Path.Combine(
                            pProjectManager.CurrentProjectDirectory, 
                            lSelectedPath, 
                            lFileName)
                        
                        If System.IO.File.Exists(lFullPath) Then
                            ShowErrorDialog($"A file named '{lFileName}' already exists in this location.")
                            ' Re-focus and select for correction
                            lNameEntry.GrabFocus()
                            lNameEntry.SelectRegion(0, lNameEntry.Text.Length)
                            Continue While
                        End If
                        
                        ' All validation passed - create the item
                        CreateNewItem(lName, vExtension, lSelectedPath, vCodeGenerator)
                        lDone = True
                    Else
                        ' User cancelled
                        lDone = True
                    End If
                End While
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ShowAddNewItemDialog error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Shows dialog for adding a new folder
        ''' </summary>
        Private Sub ShowAddNewFolderDialog()
            Try
                ' Create dialog
                Dim lDialog As New Dialog("Add New Folder", 
                                        GetTopLevelWindow(), 
                                        DialogFlags.Modal Or DialogFlags.DestroyWithParent)
                
                lDialog.SetDefaultSize(400, 120)
                
                ' Create content area
                Dim lVBox As New Box(Orientation.Vertical, 8)
                lVBox.BorderWidth = 10
                
                ' Name label and entry
                Dim lNameLabel As New Label("Folder Name:")
                lNameLabel.Halign = Align.Start
                lVBox.PackStart(lNameLabel, False, False, 0)
                
                Dim lNameEntry As New Entry()
                lNameEntry.PlaceholderText = "Enter folder name"
                ' Enable Enter key to activate default button
                lNameEntry.ActivatesDefault = True
                lVBox.PackStart(lNameEntry, False, False, 0)
                
                ' Add to content area
                lDialog.ContentArea.Add(lVBox)
                
                ' Add buttons
                lDialog.AddButton("Cancel", ResponseType.Cancel)
                Dim lCreateButton As Widget = lDialog.AddButton("Create", ResponseType.Ok)
                
                ' Set default button
                lDialog.DefaultResponse = ResponseType.Ok
                lCreateButton.CanDefault = True
                lCreateButton.GrabDefault()
                
                ' Focus name entry
                lNameEntry.GrabFocus()
                
                ' Show all
                lDialog.ShowAll()
                
                ' Keep dialog open until valid input or cancel
                Dim lDone As Boolean = False
                While Not lDone
                    Dim lResponse As ResponseType = CType(lDialog.Run(), ResponseType)
                    
                    If lResponse = ResponseType.Ok Then
                        Dim lName As String = lNameEntry.Text.Trim()
                        
                        ' Check for empty name
                        If String.IsNullOrEmpty(lName) Then
                            ShowErrorDialog("Please enter a folder name.")
                            ' Re-focus the entry and select all text for easy correction
                            lNameEntry.GrabFocus()
                            lNameEntry.SelectRegion(0, lNameEntry.Text.Length)
                            Continue While
                        End If
                        
                        ' Check for invalid folder name characters
                        Dim lInvalidChars As Char() = System.IO.Path.GetInvalidFileNameChars()
                        For Each lChar As Char In lInvalidChars
                            ' Allow path separator for nested folders
                            If lChar <> System.IO.Path.DirectorySeparatorChar AndAlso lName.Contains(lChar) Then
                                ShowErrorDialog($"Folder name contains invalid character: '{lChar}'")
                                lNameEntry.GrabFocus()
                                lNameEntry.SelectRegion(0, lNameEntry.Text.Length)
                                Continue While
                            End If
                        Next
                        
                        ' Check if folder already exists
                        Dim lParentPath As String = GetSelectedFolderPath()
                        Dim lFullPath As String = System.IO.Path.Combine(
                            pProjectManager.CurrentProjectDirectory, 
                            lParentPath, 
                            lName)
                        
                        If System.IO.Directory.Exists(lFullPath) Then
                            ShowErrorDialog($"A folder named '{lName}' already exists in this location.")
                            lNameEntry.GrabFocus()
                            lNameEntry.SelectRegion(0, lNameEntry.Text.Length)
                            Continue While
                        End If
                        
                        ' All validation passed - create the folder
                        CreateNewFolder(lName)
                        lDone = True
                    Else
                        ' User cancelled
                        lDone = True
                    End If
                End While
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ShowAddNewFolderDialog error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Shows dialog for adding existing items
        ''' </summary>
        Private Sub ShowAddExistingItemDialog()
            Try
                ' Create file chooser dialog
                Dim lDialog As New FileChooserDialog("Add Existing Item",
                                                    GetTopLevelWindow(),
                                                    FileChooserAction.Open,
                                                    "Cancel", ResponseType.Cancel,
                                                    "Add", ResponseType.Ok)
                
                ' Set multiple selection
                lDialog.SelectMultiple = True
                
                ' Add filters
                Dim lVbFilter As New FileFilter()
                lVbFilter.Name = "VB.NET Files (*.vb)"
                lVbFilter.AddPattern("*.vb")
                lDialog.AddFilter(lVbFilter)
                
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files (*.*)"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)
                
                ' Set current folder
                If pProjectManager IsNot Nothing AndAlso pProjectManager.IsProjectOpen Then
                    lDialog.SetCurrentFolder(pProjectManager.CurrentProjectDirectory)
                End If
                
                ' Run dialog
                If lDialog.Run() = ResponseType.Ok Then
                    For Each lFile As String In lDialog.Filenames
                        AddExistingItem(lFile)
                    Next
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ShowAddExistingItemDialog error: {ex.Message}")
            End Try
        End Sub
        
        ' Replace: SimpleIDE.Widgets.CustomDrawProjectExplorer.ShowAddNewItemSelector
        ''' <summary>
        ''' Shows item type selector dialog with all available item types
        ''' </summary>
        ''' <remarks>
        ''' Displays a dialog with a list of item types the user can add to the project
        ''' </remarks>
        Private Sub ShowAddNewItemSelector()
            Try
                ' Create dialog
                Dim lDialog As New Dialog("Add New Item", 
                                        GetTopLevelWindow(), 
                                        DialogFlags.Modal Or DialogFlags.DestroyWithParent)
                
                lDialog.SetDefaultSize(400, 300)
                
                ' Create list of item types
                Dim lListStore As New ListStore(GetType(String), GetType(String))
                
                ' Add all available item types
                lListStore.AppendValues("Class", "Creates a New VB.NET Class")
                lListStore.AppendValues("Module", "Creates a New VB.NET Module")
                lListStore.AppendValues("Interface", "Creates a New VB.NET Interface")
                lListStore.AppendValues("Form", "Creates a New Windows Form")
                lListStore.AppendValues("User Control", "Creates a New User Control")
                lListStore.AppendValues("Component", "Creates a New component Class")
                lListStore.AppendValues("Enum", "Creates a New enumeration")
                lListStore.AppendValues("Structure", "Creates a New Structure")
                lListStore.AppendValues("Text File", "Creates a blank text file")
                lListStore.AppendValues("XML File", "Creates a New XML file")
                lListStore.AppendValues("Configuration File", "Creates an app.config file")
                lListStore.AppendValues("Resource File", "Creates a .resx resource file")
                
                ' Create tree view
                Dim lTreeView As New TreeView(lListStore)
                lTreeView.HeadersVisible = True
                
                ' Add columns
                Dim lTypeColumn As New TreeViewColumn("Item Type", New CellRendererText(), "text", 0)
                lTypeColumn.MinWidth = 120
                lTreeView.AppendColumn(lTypeColumn)
                
                Dim lDescColumn As New TreeViewColumn("Description", New CellRendererText(), "text", 1)
                lDescColumn.Expand = True
                lTreeView.AppendColumn(lDescColumn)
                
                ' Select first item by default
                Dim lPath As New TreePath("0")
                lTreeView.Selection.SelectPath(lPath)
                
                ' Scrolled window
                Dim lScrolled As New ScrolledWindow()
                lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                lScrolled.Add(lTreeView)
                lScrolled.MinContentHeight = 200
                
                ' Add to dialog
                lDialog.ContentArea.PackStart(lScrolled, True, True, 0)
                
                ' Add buttons
                lDialog.AddButton("Cancel", ResponseType.Cancel)
                Dim lNextButton As Widget = lDialog.AddButton("Next", ResponseType.Ok)
                lDialog.DefaultResponse = ResponseType.Ok
                
                ' Enable double-click to select - use Respond method instead of Response event
                AddHandler lTreeView.RowActivated, Sub(sender, args)
                    lDialog.Respond(ResponseType.Ok)
                End Sub           
     
                ' Show all
                lDialog.ShowAll()
                
                ' Handle response
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    Dim lSelection As TreeSelection = lTreeView.Selection
                    Dim lModel As ITreeModel = Nothing
                    Dim lIter As TreeIter = Nothing
                    
                    If lSelection.GetSelected(lModel, lIter) Then
                        Dim lItemType As String = CStr(lListStore.GetValue(lIter, 0))
                        
                        ' Close this dialog first
                        lDialog.Destroy()
                        
                        ' Show specific dialog based on selection
                        Select Case lItemType
                            Case "Class"
                                ShowAddNewItemDialog("Class", ".vb", AddressOf GenerateClassCode)
                                
                            Case "Module"
                                ShowAddNewItemDialog("Module", ".vb", AddressOf GenerateModuleCode)
                                
                            Case "Interface"
                                ShowAddNewItemDialog("Interface", ".vb", AddressOf GenerateInterfaceCode)
                                
                            Case "Form"
                                ShowAddNewItemDialog("Form", ".vb", AddressOf GenerateFormCode)
                                
                            Case "User Control"
                                ShowAddNewItemDialog("UserControl", ".vb", AddressOf GenerateUserControlCode)
                                
                            Case "Component"
                                ShowAddNewItemDialog("Component", ".vb", AddressOf GenerateComponentCode)
                                
                            Case "Enum"
                                ShowAddNewItemDialog("Enum", ".vb", AddressOf GenerateEnumCode)
                                
                            Case "Structure"
                                ShowAddNewItemDialog("Structure", ".vb", AddressOf GenerateStructureCode)
                                
                            Case "Text File"
                                ShowAddNewItemDialog("TextFile", ".txt", Function(name, ns) "")
                                
                            Case "XML File"
                                ShowAddNewItemDialog("XMLFile", ".xml", Function(name, ns) "<?xml version=""1.0"" encoding=""utf-8""?>" & vbCrLf & "<root>" & vbCrLf & vbCrLf & "</root>")
                                
                            Case "Configuration File"
                                ShowAddNewItemDialog("ConfigFile", ".config", AddressOf GenerateConfigFileCode)
                                
                            Case "Resource File"
                                ShowAddNewItemDialog("ResourceFile", ".resx", AddressOf GenerateResourceFileCode)
                                
                            Case Else
                                Console.WriteLine($"Unknown item type selected: {lItemType}")
                        End Select
                    End If
                Else
                    lDialog.Destroy()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ShowAddNewItemSelector error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        ''' <summary>
        ''' Gets the selected folder path in the project explorer
        ''' </summary>
        ''' <returns>Relative path from project root</returns>
        Private Function GetSelectedFolderPath() As String
            Try
                If pSelectedNode Is Nothing OrElse pSelectedNode.Node Is Nothing Then
                    Return ""
                End If
                
                Dim lNode As ProjectNode = pSelectedNode.Node
                
                ' If a file is selected, get its parent folder
                If lNode.IsFile Then
                    Dim lDirPath As String = System.IO.Path.GetDirectoryName(lNode.Path)
                    If Not String.IsNullOrEmpty(lDirPath) Then
                        Return GetRelativePath(lDirPath)
                    End If
                    Return ""
                End If
                
                ' If a folder is selected, use it
                If Not lNode.IsFile Then
                    Return GetRelativePath(lNode.Path)
                End If
                
                Return ""
                
            Catch ex As Exception
                Console.WriteLine($"GetSelectedFolderPath error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets relative path from project root
        ''' </summary>
        ''' <param name="vFullPath">Full path to convert</param>
        ''' <returns>Relative path</returns>
        Private Function GetRelativePath(vFullPath As String) As String
            Try
                If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                    Return vFullPath
                End If
                
                Dim lProjectDir As String = pProjectManager.CurrentProjectDirectory
                
                If vFullPath.StartsWith(lProjectDir) Then
                    Dim lRelative As String = vFullPath.Substring(lProjectDir.Length)
                    If lRelative.StartsWith(System.IO.Path.DirectorySeparatorChar) Then
                        lRelative = lRelative.Substring(1)
                    End If
                    Return lRelative
                End If
                
                Return vFullPath
                
            Catch ex As Exception
                Console.WriteLine($"GetRelativePath error: {ex.Message}")
                Return vFullPath
            End Try
        End Function
        
        ''' <summary>
        ''' Validates item name
        ''' </summary>
        ''' <param name="vName">Name to validate (may include namespace dots)</param>
        ''' <returns>True if valid</returns>
        Private Function IsValidItemName(vName As String) As Boolean
            Try
                ' If it contains dots, validate each part
                If vName.Contains("."c) Then
                    Dim lParts As String() = vName.Split("."c)
                    For Each lPart As String In lParts
                        If Not IsValidSimpleName(lPart) Then
                            Return False
                        End If
                    Next
                    Return True
                Else
                    ' Simple name validation
                    Return IsValidSimpleName(vName)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"IsValidItemName error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Validates a simple name (no dots)
        ''' </summary>
        ''' <param name="vName">Name to validate</param>
        ''' <returns>True if valid</returns>
        Private Function IsValidSimpleName(vName As String) As Boolean
            Try
                If String.IsNullOrEmpty(vName) Then
                    Return False
                End If
                
                ' Check for invalid file name characters (but allow dots for qualified names)
                For Each lChar As Char In System.IO.Path.GetInvalidFileNameChars()
                    If lChar <> "."c AndAlso vName.Contains(lChar) Then
                        Return False
                    End If
                Next
                
                ' Check for VB keywords (simplified list)
                Dim lKeywords() As String = {"Class", "Module", "Interface", "Sub", "Function", "Property", "End", "If", "Then", "Else"}
                For Each lKeyword As String In lKeywords
                    If String.Equals(vName, lKeyword, StringComparison.OrdinalIgnoreCase) Then
                        Return False
                    End If
                Next
                
                ' Check first character is letter or underscore
                If Not (Char.IsLetter(vName(0)) OrElse vName(0) = "_"c) Then
                    Return False
                End If
                
                ' Check remaining characters are letters, digits, or underscores
                For i As Integer = 1 To vName.Length - 1
                    If Not (Char.IsLetterOrDigit(vName(i)) OrElse vName(i) = "_"c) Then
                        Return False
                    End If
                Next
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IsValidSimpleName error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Creates a new item and adds it to the project
        ''' </summary>
        ''' <param name="vName">Name of the item</param>
        ''' <param name="vExtension">File extension</param>
        ''' <param name="vRelativePath">Relative path from project root</param>
        ''' <param name="vCodeGenerator">Function to generate code</param>
        Private Sub CreateNewItem(vName As String, vExtension As String, vRelativePath As String, vCodeGenerator As Func(Of String, String, String))
            Try
                If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                    ShowErrorDialog("No project Is currently open")
                    Return
                End If
                
                ' Parse the name to check for namespace/folder specification
                Dim lParsedInfo As Tuple(Of String, String, String) = ParseQualifiedName(vName, vRelativePath)
                Dim lActualName As String = lParsedInfo.Item1        ' Just the class/item name
                Dim lFinalPath As String = lParsedInfo.Item2         ' Combined path including any from the name
                Dim lExplicitNamespace As String = lParsedInfo.Item3 ' Any explicit namespace specified
                
                ' Build full file path
                Dim lFileName As String = lActualName & vExtension
                Dim lFullPath As String = System.IO.Path.Combine(pProjectManager.CurrentProjectDirectory, lFinalPath, lFileName)
                
                ' Check if file already exists
                If System.IO.File.Exists(lFullPath) Then
                    ShowErrorDialog($"A file named '{lFileName}' already exists in this location")
                    Return
                End If
                
                ' Get namespace for the file location (use explicit if provided, otherwise calculate)
                Dim lNamespace As String
                If Not String.IsNullOrEmpty(lExplicitNamespace) Then
                    lNamespace = lExplicitNamespace
                Else
                    lNamespace = GetNamespaceForPath(lFinalPath)
                End If
                
                ' Generate code content
                Dim lContent As String = vCodeGenerator(lActualName, lNamespace)
                
                ' Create directory if needed
                Dim lDirectory As String = System.IO.Path.GetDirectoryName(lFullPath)
                If Not System.IO.Directory.Exists(lDirectory) Then
                    System.IO.Directory.CreateDirectory(lDirectory)
                End If
                
                ' Write file
                System.IO.File.WriteAllText(lFullPath, lContent)
                
                ' CRITICAL FIX: Add to project - use the overload that returns DocumentModel
                ' First, try to get or create the DocumentModel
                Dim lDocModel As DocumentModel = pProjectManager.GetDocumentModel(lFullPath)
                If lDocModel Is Nothing Then
                    ' File not in project yet - add it
                    lDocModel = pProjectManager.AddFileToProject(lFullPath)
                End If
                
                ' Check if successfully added
                If lDocModel IsNot Nothing Then
                    Console.WriteLine($"Added new {lActualName}{vExtension} to project at {lFinalPath}")
                    
                    ' Refresh project explorer by calling LoadProjectFromManager
                    ' This will reload the tree from the ProjectManager's current state
                    LoadProjectFromManager()
                    
                    ' Raise event to open the new file
                    RaiseEvent FileSelected(lFullPath)
                Else
                    Console.WriteLine($"Failed to add {lFileName} to project")
                    ShowErrorDialog($"Failed to add {lFileName} to project")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CreateNewItem error: {ex.Message}")
                ShowErrorDialog($"Failed to create new item: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Parses a potentially qualified name to extract class name and path components
        ''' </summary>
        ''' <param name="vQualifiedName">The name entered by user (e.g. "MyNamespace.SubFolder.ClassName")</param>
        ''' <param name="vBasePath">The base path from selected location</param>
        ''' <returns>Tuple of (ClassName, FinalPath, ExplicitNamespace)</returns>
        Private Function ParseQualifiedName(vQualifiedName As String, vBasePath As String) As Tuple(Of String, String, String)
            Try
                ' Check if the name contains dots (namespace/folder separators)
                If Not vQualifiedName.Contains("."c) Then
                    ' Simple name, no namespace specified
                    Return New Tuple(Of String, String, String)(vQualifiedName, vBasePath, "")
                End If
                
                ' Split by dots
                Dim lParts As String() = vQualifiedName.Split("."c)
                Dim lClassName As String = lParts(lParts.Length - 1) ' Last part is the class name
                
                ' Get the project's root namespace
                Dim lRootNamespace As String = GetProjectRootNamespace()
                
                ' Build the namespace/folder parts (everything except the class name)
                Dim lNamespaceParts As New List(Of String)()
                for i As Integer = 0 To lParts.Length - 2
                    lNamespaceParts.Add(lParts(i))
                Next
                
                ' Check if the user specified the root namespace
                Dim lFolderParts As New List(Of String)()
                Dim lExplicitNamespace As String = ""
                Dim lStartIndex As Integer = 0
                
                ' If the first part matches the root namespace, skip it for folder creation
                If lNamespaceParts.Count > 0 AndAlso 
                   String.Equals(lNamespaceParts(0), lRootNamespace, StringComparison.OrdinalIgnoreCase) Then
                    lStartIndex = 1
                    ' User explicitly specified the namespace
                    lExplicitNamespace = String.Join(".", lNamespaceParts)
                End If
                
                ' Build folder structure from remaining parts
                for i As Integer = lStartIndex To lNamespaceParts.Count - 1
                    lFolderParts.Add(lNamespaceParts(i))
                Next
                
                ' Combine base path with folder parts
                Dim lFinalPath As String = vBasePath
                If lFolderParts.Count > 0 Then
                    Dim lAdditionalPath As String = String.Join(System.IO.Path.DirectorySeparatorChar, lFolderParts)
                    If String.IsNullOrEmpty(lFinalPath) Then
                        lFinalPath = lAdditionalPath
                    Else
                        lFinalPath = System.IO.Path.Combine(lFinalPath, lAdditionalPath)
                    End If
                End If
                
                Return New Tuple(Of String, String, String)(lClassName, lFinalPath, lExplicitNamespace)
                
            Catch ex As Exception
                Console.WriteLine($"ParseQualifiedName error: {ex.Message}")
                ' On error, treat as simple name
                Return New Tuple(Of String, String, String)(vQualifiedName, vBasePath, "")
            End Try
        End Function
        
        ''' <summary>
        ''' Creates a new folder in the project
        ''' </summary>
        ''' <param name="vName">Folder name</param>
        Private Sub CreateNewFolder(vName As String)
            Try
                If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                    ShowErrorDialog("No project is currently open")
                    Return
                End If
                
                ' Get selected folder path
                Dim lParentPath As String = GetSelectedFolderPath()
                Dim lFullPath As String = System.IO.Path.Combine(pProjectManager.CurrentProjectDirectory, lParentPath, vName)
                
                ' Check if folder already exists
                If System.IO.Directory.Exists(lFullPath) Then
                    ShowErrorDialog($"A folder named '{vName}' already exists in this location")
                    Return
                End If
                
                ' Create folder
                System.IO.Directory.CreateDirectory(lFullPath)
                
                Console.WriteLine($"Created New folder: {lFullPath}")
                
                ' Refresh project explorer
                pProjectManager.LoadProject(pProjectFile)
                
                ' Raise project modified event
                RaiseEvent ProjectModified()
                
            Catch ex As Exception
                Console.WriteLine($"CreateNewFolder error: {ex.Message}")
                ShowErrorDialog($"Failed To create folder: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Adds an existing item to the project
        ''' </summary>
        ''' <param name="vFilePath">Path to existing file</param>
        Private Sub AddExistingItem(vFilePath As String)
            Try
                If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                    ShowErrorDialog("No project Is currently open")
                    Return
                End If
                
                ' CRITICAL FIX: Add to project - use the overload that returns DocumentModel
                ' First, try to get or create the DocumentModel
                Dim lDocModel As DocumentModel = pProjectManager.GetDocumentModel(vFilePath)
                If lDocModel Is Nothing Then
                    ' File not in project yet - add it
                    lDocModel = pProjectManager.AddFileToProject(vFilePath)
                End If
                
                ' Check if successfully added
                If lDocModel IsNot Nothing Then
                    Console.WriteLine($"Added existing file To project: {vFilePath}")
                    
                    ' Refresh project explorer by calling LoadProjectFromManager
                    ' This will reload the tree from the ProjectManager's current state
                    LoadProjectFromManager()
                    
                    ' Raise project modified event
                    RaiseEvent ProjectModified()
                    
                    ' Open the file in the editor
                    RaiseEvent FileSelected(vFilePath)
                Else
                    Console.WriteLine($"Failed To add existing file To project: {vFilePath}")
                    ShowErrorDialog($"Failed To add file To project")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"AddExistingItem error: {ex.Message}")
                ShowErrorDialog($"Failed To add existing item: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the namespace declaration string for a given path in the project
        ''' </summary>
        ''' <param name="vRelativePath">Relative path from project root</param>
        ''' <returns>Namespace declaration string (empty for root, relative for subfolders)</returns>
        ''' <remarks>
        ''' This method handles implicit root namespace correctly:
        ''' - Returns empty string for root folder (no namespace declaration needed)
        ''' - Returns relative namespace for subfolders (e.g., "Managers" not "SimpleIDE.Managers")
        ''' </remarks>
        Private Function GetNamespaceForPath(vRelativePath As String) As String
            Try
                ' If no relative path, we're in the root - no namespace declaration needed
                If String.IsNullOrEmpty(vRelativePath) Then
                    Return ""  ' No namespace declaration for root
                End If
                
                ' Convert path to namespace (replace separators with dots)
                ' This will be the relative namespace from root
                Dim lPathParts As String() = vRelativePath.Split(System.IO.Path.DirectorySeparatorChar)
                Dim lNamespaceParts As New List(Of String)()
                
                For Each lPart As String In lPathParts
                    If Not String.IsNullOrEmpty(lPart) Then
                        lNamespaceParts.Add(lPart)
                    End If
                Next
                
                ' Return the relative namespace path (not including root namespace)
                ' e.g., for "Managers" folder, return "Managers" not "SimpleIDE.Managers"
                Return String.Join(".", lNamespaceParts)
                
            Catch ex As Exception
                Console.WriteLine($"GetNamespaceForPath error: {ex.Message}")
                Return ""
            End Try
        End Function
        
        ''' <summary>
        ''' Gets the root namespace from the project file
        ''' </summary>
        ''' <returns>Root namespace or project name</returns>
        Private Function GetProjectRootNamespace() As String
            Try
                If pProjectManager IsNot Nothing AndAlso pProjectManager.IsProjectOpen Then
                    ' Try to read root namespace from project file
                    Dim lProjectPath As String = pProjectManager.CurrentProjectPath
                    If File.Exists(lProjectPath) Then
                        Dim lDoc As New Xml.XmlDocument()
                        lDoc.Load(lProjectPath)
                        
                        ' Look for RootNamespace element
                        Dim lRootNamespaceNode As Xml.XmlNode = lDoc.SelectSingleNode("//RootNamespace")
                        If lRootNamespaceNode IsNot Nothing AndAlso Not String.IsNullOrEmpty(lRootNamespaceNode.InnerText) Then
                            Return lRootNamespaceNode.InnerText
                        End If
                    End If
                    
                    ' Fall back to project name without extension
                    Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(pProjectManager.CurrentProjectPath)
                    Return lProjectName
                End If
                
                Return "MyApplication"
                
            Catch ex As Exception
                Console.WriteLine($"GetProjectRootNamespace error: {ex.Message}")
                Return "MyApplication"
            End Try
        End Function
        
        ''' <summary>
        ''' Shows error dialog
        ''' </summary>
        ''' <param name="vMessage">Error message</param>
        Private Sub ShowErrorDialog(vMessage As String)
            Try
                Dim lDialog As New MessageDialog(GetTopLevelWindow(),
                                                DialogFlags.Modal Or DialogFlags.DestroyWithParent,
                                                MessageType.Error,
                                                ButtonsType.Ok,
                                                vMessage)
                lDialog.Run()
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"ShowErrorDialog error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Gets the top level window
        ''' </summary>
        ''' <returns>Top level window or Nothing</returns>
        Private Function GetTopLevelWindow() As Window
            Try
                Dim lParent As Widget = Me.Parent
                While lParent IsNot Nothing
                    If TypeOf lParent Is Window Then
                        Return DirectCast(lParent, Window)
                    End If
                    lParent = lParent.Parent
                End While
                
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetTopLevelWindow error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ' ===== Code Generation Methods =====
        
        ' Replace: SimpleIDE.Widgets.CustomDrawProjectExplorer.GenerateClassCode
        ''' <summary>
        ''' Generates code for a new class with proper namespace handling
        ''' </summary>
        ''' <param name="vClassName">Name of the class</param>
        ''' <param name="vNamespace">Relative namespace path (empty for root)</param>
        ''' <returns>Generated code following VB.NET implicit root namespace rules</returns>
        Private Function GenerateClassCode(vClassName As String, vNamespace As String) As String
            Dim lBuilder As New System.Text.StringBuilder()
            
            lBuilder.AppendLine("' " & vClassName & ".vb")
            lBuilder.AppendLine("' Created: " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            lBuilder.AppendLine()
            lBuilder.AppendLine("Imports System")
            lBuilder.AppendLine()
            
            ' Only declare namespace if NOT in root (following implicit root namespace rules)
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine($"Namespace {vNamespace}")
                lBuilder.AppendLine()
            End If
            
            ' Add proper indentation based on whether we're in a namespace
            Dim lIndent As String = If(String.IsNullOrEmpty(vNamespace), "", "    ")
            
            lBuilder.AppendLine($"{lIndent}''' <summary>")
            lBuilder.AppendLine($"{lIndent}''' {vClassName} class implementation")
            lBuilder.AppendLine($"{lIndent}''' </summary>")
            lBuilder.AppendLine($"{lIndent}Public Class {vClassName}")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"{lIndent}    ''' <summary>")
            lBuilder.AppendLine($"{lIndent}    ''' Initializes a new instance of the {vClassName} class")
            lBuilder.AppendLine($"{lIndent}    ''' </summary>")
            lBuilder.AppendLine($"{lIndent}    Public Sub New()")
            lBuilder.AppendLine($"{lIndent}        ' TODO: Initialize your class here")
            lBuilder.AppendLine($"{lIndent}    End Sub")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"{lIndent}End Class")
            
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine()
                lBuilder.AppendLine("End Namespace")
            End If
            
            Return lBuilder.ToString()
        End Function
        
        ''' <summary>
        ''' Generates code for a new module with proper namespace handling
        ''' </summary>
        ''' <param name="vModuleName">Name of the module</param>
        ''' <param name="vNamespace">Relative namespace path (empty for root)</param>
        ''' <returns>Generated code following VB.NET implicit root namespace rules</returns>
        Private Function GenerateModuleCode(vModuleName As String, vNamespace As String) As String
            Dim lBuilder As New System.Text.StringBuilder()
            
            lBuilder.AppendLine("' " & vModuleName & ".vb")
            lBuilder.AppendLine("' Created: " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            lBuilder.AppendLine()
            lBuilder.AppendLine("Imports System")
            lBuilder.AppendLine()
            
            ' Only declare namespace if NOT in root (following implicit root namespace rules)
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine($"Namespace {vNamespace}")
                lBuilder.AppendLine()
            End If
            
            ' Add proper indentation based on whether we're in a namespace
            Dim lIndent As String = If(String.IsNullOrEmpty(vNamespace), "", "    ")
            
            lBuilder.AppendLine($"{lIndent}''' <summary>")
            lBuilder.AppendLine($"{lIndent}''' {vModuleName} module implementation")
            lBuilder.AppendLine($"{lIndent}''' </summary>")
            lBuilder.AppendLine($"{lIndent}Public Module {vModuleName}")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"{lIndent}    ' TODO: Add your module members here")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"{lIndent}End Module")
            
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine()
                lBuilder.AppendLine("End Namespace")
            End If
            
            Return lBuilder.ToString()
        End Function
        
        ' Replace: SimpleIDE.Widgets.CustomDrawProjectExplorer.GenerateInterfaceCode
        ''' <summary>
        ''' Generates code for a new interface with proper namespace handling
        ''' </summary>
        ''' <param name="vInterfaceName">Name of the interface</param>
        ''' <param name="vNamespace">Relative namespace path (empty for root)</param>
        ''' <returns>Generated code following VB.NET implicit root namespace rules</returns>
        Private Function GenerateInterfaceCode(vInterfaceName As String, vNamespace As String) As String
            Dim lBuilder As New System.Text.StringBuilder()
            
            ' Ensure interface name starts with I
            Dim lInterfaceName As String = vInterfaceName
            If Not lInterfaceName.StartsWith("I") Then
                lInterfaceName = "I" & lInterfaceName
            End If
            
            lBuilder.AppendLine("' " & lInterfaceName & ".vb")
            lBuilder.AppendLine("' Created: " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            lBuilder.AppendLine()
            lBuilder.AppendLine("Imports System")
            lBuilder.AppendLine()
            
            ' Only declare namespace if NOT in root (following implicit root namespace rules)
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine($"Namespace {vNamespace}")
                lBuilder.AppendLine()
            End If
            
            ' Add proper indentation based on whether we're in a namespace
            Dim lIndent As String = If(String.IsNullOrEmpty(vNamespace), "", "    ")
            
            lBuilder.AppendLine($"{lIndent}''' <summary>")
            lBuilder.AppendLine($"{lIndent}''' {lInterfaceName} interface definition")
            lBuilder.AppendLine($"{lIndent}''' </summary>")
            lBuilder.AppendLine($"{lIndent}Public Interface {lInterfaceName}")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"{lIndent}    ' TODO: Define your interface members here")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"{lIndent}End Interface")
            
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine()
                lBuilder.AppendLine("End Namespace")
            End If
            
            Return lBuilder.ToString()
        End Function
        
        ''' <summary>
        ''' Generates code for a new Windows Form
        ''' </summary>
        ''' <param name="vFormName">Name of the form</param>
        ''' <param name="vNamespace">Full namespace for the form</param>
        ''' <returns>Generated code</returns>
        Private Function GenerateFormCode(vFormName As String, vNamespace As String) As String
            Dim lBuilder As New System.Text.StringBuilder()
            
            lBuilder.AppendLine("' " & vFormName & ".vb")
            lBuilder.AppendLine("' Created: " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            lBuilder.AppendLine()
            lBuilder.AppendLine("Imports Gtk")
            lBuilder.AppendLine("Imports System")
            lBuilder.AppendLine()
            
            ' Always declare the full namespace for clarity and portability
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine($"Namespace {vNamespace}")
                lBuilder.AppendLine()
            End If
            
            lBuilder.AppendLine($"    ''' <summary>")
            lBuilder.AppendLine($"    ''' {vFormName} window implementation")
            lBuilder.AppendLine($"    ''' </summary>")
            lBuilder.AppendLine($"    Public Class {vFormName}")
            lBuilder.AppendLine($"        Inherits Window")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ''' <summary>")
            lBuilder.AppendLine($"        ''' Initializes a new instance of the {vFormName} class")
            lBuilder.AppendLine($"        ''' </summary>")
            lBuilder.AppendLine($"        Public Sub New()")
            lBuilder.AppendLine($"            MyBase.New(""{vFormName}"")")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"            ' Set window properties")
            lBuilder.AppendLine($"            SetDefaultSize(600, 400)")
            lBuilder.AppendLine($"            SetPosition(WindowPosition.Center)")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"            ' Initialize components")
            lBuilder.AppendLine($"            InitializeComponents()")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"            ' Show all widgets")
            lBuilder.AppendLine($"            ShowAll()")
            lBuilder.AppendLine($"        End Sub")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ''' <summary>")
            lBuilder.AppendLine($"        ''' Initializes the form components")
            lBuilder.AppendLine($"        ''' </summary>")
            lBuilder.AppendLine($"        Private Sub InitializeComponents()")
            lBuilder.AppendLine($"            Try")
            lBuilder.AppendLine($"                ' TODO: Add your form controls here")
            lBuilder.AppendLine($"                Dim lMainBox As New VBox(False, 5)")
            lBuilder.AppendLine($"                lMainBox.BorderWidth = 10")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"                Add(lMainBox)")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"            Catch ex As Exception")
            lBuilder.AppendLine($"                Console.WriteLine($""InitializeComponents error: {{ex.Message}}"")")
            lBuilder.AppendLine($"            End Try")
            lBuilder.AppendLine($"        End Sub")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"    End Class")
            
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine()
                lBuilder.AppendLine("End Namespace")
            End If
            
            Return lBuilder.ToString()
        End Function
        
        ''' <summary>
        ''' Generates code for a new User Control
        ''' </summary>
        ''' <param name="vControlName">Name of the control</param>
        ''' <param name="vNamespace">Full namespace for the control</param>
        ''' <returns>Generated code</returns>
        Private Function GenerateUserControlCode(vControlName As String, vNamespace As String) As String
            Dim lBuilder As New System.Text.StringBuilder()
            
            lBuilder.AppendLine("' " & vControlName & ".vb")
            lBuilder.AppendLine("' Created: " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            lBuilder.AppendLine()
            lBuilder.AppendLine("Imports Gtk")
            lBuilder.AppendLine("Imports System")
            lBuilder.AppendLine()
            
            ' Always declare the full namespace for clarity and portability
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine($"Namespace {vNamespace}")
                lBuilder.AppendLine()
            End If
            
            lBuilder.AppendLine($"    ''' <summary>")
            lBuilder.AppendLine($"    ''' {vControlName} user control implementation")
            lBuilder.AppendLine($"    ''' </summary>")
            lBuilder.AppendLine($"    Public Class {vControlName}")
            lBuilder.AppendLine($"        Inherits Box")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ' ===== Private Fields =====")
            lBuilder.AppendLine($"        ' TODO: Add your private fields here")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ' ===== Events =====")
            lBuilder.AppendLine($"        ' TODO: Define custom events here")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ' ===== Properties =====")
            lBuilder.AppendLine($"        ' TODO: Add custom properties here")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ''' <summary>")
            lBuilder.AppendLine($"        ''' Initializes a new instance of the {vControlName} class")
            lBuilder.AppendLine($"        ''' </summary>")
            lBuilder.AppendLine($"        Public Sub New()")
            lBuilder.AppendLine($"            MyBase.New(Orientation.Vertical, 5)")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"            ' Initialize components")
            lBuilder.AppendLine($"            InitializeComponents()")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"            ' Show all widgets")
            lBuilder.AppendLine($"            ShowAll()")
            lBuilder.AppendLine($"        End Sub")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ''' <summary>")
            lBuilder.AppendLine($"        ''' Initializes the control components")
            lBuilder.AppendLine($"        ''' </summary>")
            lBuilder.AppendLine($"        Private Sub InitializeComponents()")
            lBuilder.AppendLine($"            Try")
            lBuilder.AppendLine($"                ' Set control properties")
            lBuilder.AppendLine($"                BorderWidth = 5")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"                ' TODO: Add your control elements here")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"            Catch ex As Exception")
            lBuilder.AppendLine($"                Console.WriteLine($""InitializeComponents error: {{ex.Message}}"")")
            lBuilder.AppendLine($"            End Try")
            lBuilder.AppendLine($"        End Sub")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"    End Class")
            
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine()
                lBuilder.AppendLine("End Namespace")
            End If
            
            Return lBuilder.ToString()
        End Function

        ''' <summary>
        ''' Generates code for a new enumeration
        ''' </summary>
        ''' <param name="vName">Name of the enum</param>
        ''' <param name="vNamespace">Namespace for the enum</param>
        ''' <returns>Generated VB.NET code for the enum</returns>
        Private Function GenerateEnumCode(vName As String, vNamespace As String) As String
            Dim lBuilder As New System.Text.StringBuilder()
            
            ' Add header comment
            lBuilder.AppendLine($"' {vName}.vb")
            lBuilder.AppendLine($"' Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            lBuilder.AppendLine()
            
            ' Add imports
            lBuilder.AppendLine("Imports System")
            lBuilder.AppendLine()
            
            ' Add namespace if specified
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine($"Namespace {vNamespace}")
                lBuilder.AppendLine()
            End If
            
            ' Add XML documentation
            lBuilder.AppendLine($"    ''' <summary>")
            lBuilder.AppendLine($"    ''' Represents the {vName} enumeration")
            lBuilder.AppendLine($"    ''' </summary>")
            lBuilder.AppendLine($"    Public Enum {vName}")
            lBuilder.AppendLine($"        eUnspecified")
            lBuilder.AppendLine($"        eFirstValue")
            lBuilder.AppendLine($"        eSecondValue")
            lBuilder.AppendLine($"        eLastValue")
            lBuilder.AppendLine($"    End Enum")
            
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine()
                lBuilder.AppendLine("End Namespace")
            End If
            
            Return lBuilder.ToString()
        End Function
        
        ''' <summary>
        ''' Generates code for a new structure
        ''' </summary>
        ''' <param name="vName">Name of the structure</param>
        ''' <param name="vNamespace">Namespace for the structure</param>
        ''' <returns>Generated VB.NET code for the structure</returns>
        Private Function GenerateStructureCode(vName As String, vNamespace As String) As String
            Dim lBuilder As New System.Text.StringBuilder()
            
            ' Add header comment
            lBuilder.AppendLine($"' {vName}.vb")
            lBuilder.AppendLine($"' Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            lBuilder.AppendLine()
            
            ' Add imports
            lBuilder.AppendLine("Imports System")
            lBuilder.AppendLine()
            
            ' Add namespace if specified
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine($"Namespace {vNamespace}")
                lBuilder.AppendLine()
            End If
            
            ' Add XML documentation
            lBuilder.AppendLine($"    ''' <summary>")
            lBuilder.AppendLine($"    ''' Represents the {vName} structure")
            lBuilder.AppendLine($"    ''' </summary>")
            lBuilder.AppendLine($"    Public Structure {vName}")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ' ===== Private Fields =====")
            lBuilder.AppendLine($"        Private pValue As String")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ' ===== Properties =====")
            lBuilder.AppendLine($"        ''' <summary>")
            lBuilder.AppendLine($"        ''' Gets or sets the value")
            lBuilder.AppendLine($"        ''' </summary>")
            lBuilder.AppendLine($"        Public Property Value As String")
            lBuilder.AppendLine($"            Get")
            lBuilder.AppendLine($"                Return pValue")
            lBuilder.AppendLine($"            End Get")
            lBuilder.AppendLine($"            Set(value As String)")
            lBuilder.AppendLine($"                pValue = value")
            lBuilder.AppendLine($"            End Set")
            lBuilder.AppendLine($"        End Property")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"    End Structure")
            
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine()
                lBuilder.AppendLine("End Namespace")
            End If
            
            Return lBuilder.ToString()
        End Function
        
        ''' <summary>
        ''' Generates code for a new component class
        ''' </summary>
        ''' <param name="vName">Name of the component</param>
        ''' <param name="vNamespace">Namespace for the component</param>
        ''' <returns>Generated VB.NET code for the component</returns>
        Private Function GenerateComponentCode(vName As String, vNamespace As String) As String
            Dim lBuilder As New System.Text.StringBuilder()
            
            ' Add header comment
            lBuilder.AppendLine($"' {vName}.vb")
            lBuilder.AppendLine($"' Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            lBuilder.AppendLine()
            
            ' Add imports
            lBuilder.AppendLine("Imports System")
            lBuilder.AppendLine("Imports System.ComponentModel")
            lBuilder.AppendLine()
            
            ' Add namespace if specified
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine($"Namespace {vNamespace}")
                lBuilder.AppendLine()
            End If
            
            ' Add XML documentation
            lBuilder.AppendLine($"    ''' <summary>")
            lBuilder.AppendLine($"    ''' Represents the {vName} component")
            lBuilder.AppendLine($"    ''' </summary>")
            lBuilder.AppendLine($"    Public Class {vName}")
            lBuilder.AppendLine($"        Inherits Component")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ''' <summary>")
            lBuilder.AppendLine($"        ''' Initializes a new instance of the {vName} class")
            lBuilder.AppendLine($"        ''' </summary>")
            lBuilder.AppendLine($"        Public Sub New()")
            lBuilder.AppendLine($"            MyBase.New()")
            lBuilder.AppendLine($"            InitializeComponent()")
            lBuilder.AppendLine($"        End Sub")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"        ''' <summary>")
            lBuilder.AppendLine($"        ''' Initializes the component")
            lBuilder.AppendLine($"        ''' </summary>")
            lBuilder.AppendLine($"        Private Sub InitializeComponent()")
            lBuilder.AppendLine($"            ' TODO: Add initialization code here")
            lBuilder.AppendLine($"        End Sub")
            lBuilder.AppendLine()
            lBuilder.AppendLine($"    End Class")
            
            If Not String.IsNullOrEmpty(vNamespace) Then
                lBuilder.AppendLine()
                lBuilder.AppendLine("End Namespace")
            End If
            
            Return lBuilder.ToString()
        End Function
        
        ''' <summary>
        ''' Generates content for a configuration file
        ''' </summary>
        ''' <param name="vName">Name of the file (not used)</param>
        ''' <param name="vNamespace">Namespace (not used)</param>
        ''' <returns>Generated XML configuration content</returns>
        Private Function GenerateConfigFileCode(vName As String, vNamespace As String) As String
            Dim lBuilder As New System.Text.StringBuilder()
            
            lBuilder.AppendLine("<?xml version=""1.0"" encoding=""utf-8""?>")
            lBuilder.AppendLine("<configuration>")
            lBuilder.AppendLine("    <startup>")
            lBuilder.AppendLine("        <supportedRuntime version=""v4.0"" sku="".NETFramework,Version=v4.8""/>")
            lBuilder.AppendLine("    </startup>")
            lBuilder.AppendLine("    ")
            lBuilder.AppendLine("    <appSettings>")
            lBuilder.AppendLine("        <!-- Add application settings here -->")
            lBuilder.AppendLine("    </appSettings>")
            lBuilder.AppendLine("    ")
            lBuilder.AppendLine("    <connectionStrings>")
            lBuilder.AppendLine("        <!-- Add connection strings here -->")
            lBuilder.AppendLine("    </connectionStrings>")
            lBuilder.AppendLine("</configuration>")
            
            Return lBuilder.ToString()
        End Function
        
        ''' <summary>
        ''' Generates content for a resource file
        ''' </summary>
        ''' <param name="vName">Name of the file (not used)</param>
        ''' <param name="vNamespace">Namespace (not used)</param>
        ''' <returns>Generated XML resource content</returns>
        Private Function GenerateResourceFileCode(vName As String, vNamespace As String) As String
            ' For .resx files, return minimal valid XML
            ' The actual resource editing would need a dedicated editor
            Dim lBuilder As New System.Text.StringBuilder()
            
            lBuilder.AppendLine("<?xml version=""1.0"" encoding=""utf-8""?>")
            lBuilder.AppendLine("<root>")
            lBuilder.AppendLine("  <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">")
            lBuilder.AppendLine("    <xsd:element name=""root"" msdata:IsDataSet=""True"">")
            lBuilder.AppendLine("    </xsd:element>")
            lBuilder.AppendLine("  </xsd:schema>")
            lBuilder.AppendLine("  <resheader name=""resmimetype"">")
            lBuilder.AppendLine("    <value>text/microsoft-resx</value>")
            lBuilder.AppendLine("  </resheader>")
            lBuilder.AppendLine("  <resheader name=""version"">")
            lBuilder.AppendLine("    <value>2.0</value>")
            lBuilder.AppendLine("  </resheader>")
            lBuilder.AppendLine("  <!-- Add resources here -->")
            lBuilder.AppendLine("</root>")
            
            Return lBuilder.ToString()
        End Function
        
    End Class
    
End Namespace
