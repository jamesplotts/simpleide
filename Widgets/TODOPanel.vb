' Widgets/TODOPanel.vb - Enhanced TODO panel with filtering, priorities, and AI integration
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets
    Public Class TodoPanel
        Inherits Box
        
        ' Private fields - UI
        Private pToolbar As Toolbar
        Private pFilterToolbar As Toolbar
        Private pTreeView As TreeView
        Private pListStore As ListStore
        Private pScrolledWindow As ScrolledWindow
        Private pContextMenu As Menu
        Private pStatusBar As Label

        
        ' Filter controls
        Private pSearchEntry As SearchEntry
        Private pPriorityCombo As ComboBoxText
        Private pCategoryCombo As ComboBoxText
        Private pStatusCombo As ComboBoxText
        Private pOverdueToggle As ToggleToolButton
        Private pRefreshButton As ToolButton
        Private pAddButton As ToolButton
        
        ' Private fields - Data
        Private pTODOManager As TODOManager = Nothing
        Private pAllTODOs As New List(Of TODOItem)
        Private pFilteredTODOs As New List(Of TODOItem)
        Private pSelectedTODO As TODOItem
        
        ' Events
        Public Event TodoSelected(vTODO As TODOItem)
        Public Event TODODoubleClicked(vTODO As TODOItem)
        Public Event SendToAI(vTODO As TODOItem)
        Public Event NavigateToCode(vFilePath As String, vLine As Integer, vColumn As Integer)
        
        Public Sub New()
            MyBase.New(Orientation.Vertical, 0)
            Initialize()
        End Sub
        
        Private Sub Initialize()
            Try
                BuildUI()
                CreateContextMenu()
                ConnectEvents()
                
                ' Show empty state initially
                UpdateStatusBar()
                ShowEmptyState()
                ShowAll()
            Console.WriteLine($"TODO Intialized")
            Catch ex As Exception
                Console.WriteLine($"error initializing TodoPanel: {ex.Message}")
            End Try
        End Sub
        
        Private Sub BuildUI()
            Console.WriteLine($"TodoPanel.vb - BuildUI()")
            ' Main toolbar
            pToolbar = New Toolbar()
            pToolbar.ToolbarStyle = ToolbarStyle.Icons
            pToolbar.IconSize = IconSize.SmallToolbar
            
            ' Add TODO button
            pAddButton = New ToolButton(Nothing, "Add")
            pAddButton.IconWidget = Image.NewFromIconName("list-add-symbolic", IconSize.SmallToolbar)
            pAddButton.TooltipText = "Add New TODO item"
            pToolbar.Insert(pAddButton, -1)
            
            ' Refresh button
            pRefreshButton = New ToolButton(Nothing, "Refresh")
            pRefreshButton.IconWidget = Image.NewFromIconName("view-Refresh-symbolic", IconSize.SmallToolbar)
            pRefreshButton.TooltipText = "Refresh TODO list and scan codebase"
            pToolbar.Insert(pRefreshButton, -1)
            
            ' Separator
            pToolbar.Insert(New SeparatorToolItem(), -1)
            
            
            ' Search entry
            Dim lSearchItem As New ToolItem()
            pSearchEntry = New SearchEntry()
            pSearchEntry.PlaceholderText = "Search TODOs..."
            pSearchEntry.WidthRequest = 150
            lSearchItem.Add(pSearchEntry)
            pToolbar.Insert(lSearchItem, -1)
            
            ' Priority filter
            Dim lPriorityItem As New ToolItem()
            pPriorityCombo = New ComboBoxText()
            pPriorityCombo.AppendText("All Priorities")
            pPriorityCombo.AppendText("Critical")
            pPriorityCombo.AppendText("High")
            pPriorityCombo.AppendText("Medium")
            pPriorityCombo.AppendText("Low")
            pPriorityCombo.Active = 0
            pPriorityCombo.WidthRequest = 120
            lPriorityItem.Add(pPriorityCombo)
            pToolbar.Insert(lPriorityItem, -1)
            
            ' Category filter
            Dim lCategoryItem As New ToolItem()
            pCategoryCombo = New ComboBoxText()
            pCategoryCombo.AppendText("All Categories")
            pCategoryCombo.AppendText("Bug")
            pCategoryCombo.AppendText("Feature")
            pCategoryCombo.AppendText("documentation")
            pCategoryCombo.AppendText("Refactor")
            pCategoryCombo.AppendText("Testing")
            pCategoryCombo.AppendText("Performance")
            pCategoryCombo.AppendText("Security")
            pCategoryCombo.AppendText("UI/UX")
            pCategoryCombo.AppendText("Other")
            pCategoryCombo.Active = 0
            pCategoryCombo.WidthRequest = 120
            lCategoryItem.Add(pCategoryCombo)
            pToolbar.Insert(lCategoryItem, -1)
            
            ' Status filter
            Dim lStatusItem As New ToolItem()
            pStatusCombo = New ComboBoxText()
            pStatusCombo.AppendText("All Status")
            pStatusCombo.AppendText("Pending")
            pStatusCombo.AppendText("in Progress")
            pStatusCombo.AppendText("Completed")
            pStatusCombo.AppendText("Cancelled")
            pStatusCombo.Active = 0
            pStatusCombo.WidthRequest = 120
            lStatusItem.Add(pStatusCombo)
            pToolbar.Insert(lStatusItem, -1)
            
            ' Overdue toggle
            pOverdueToggle = New ToggleToolButton()
            pOverdueToggle.IconWidget = Image.NewFromIconName("alarm-symbolic", IconSize.SmallToolbar)
            pOverdueToggle.TooltipText = "Show only overdue items"
            pToolbar.Insert(pOverdueToggle, -1)
            
            
            ' Scrolled window for tree view
            pScrolledWindow = New ScrolledWindow()
            pScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
            
            ' Create tree view and list store (must be done before adding to scrolled window)
            CreateTreeView()
            
            ' Status bar
            pStatusBar = New Label("Ready")
            pStatusBar.Halign = Align.Start
            pStatusBar.MarginStart = 6
            pStatusBar.MarginEnd = 6
            pStatusBar.MarginTop = 3
            pStatusBar.MarginBottom = 3
            
            ' Pack components
            PackStart(pToolbar, False, False, 0)
            PackStart(pFilterToolbar, False, False, 0)
            PackStart(New Separator(Orientation.Horizontal), False, False, 0)
            PackStart(pScrolledWindow, True, True, 0)
            PackStart(pStatusBar, False, False, 0)
            
            'ShowAll()
        End Sub

        
        Private Sub CreateTreeView()
            Console.WriteLine($"TodoPanel.vb - CreateTreeView()")
            ' Create list store: Priority Icon, Category Icon, Title, Priority Text, Category Text, Status, Due Date, Description, ID
            pListStore = New ListStore(
                GetType(Gdk.Pixbuf),     ' 0: Priority Icon
                GetType(Gdk.Pixbuf),     ' 1: Category Icon
                GetType(String),         ' 2: Title (with Progress indicators)
                GetType(String),         ' 3: Priority Text
                GetType(String),         ' 4: Category Text
                GetType(String),         ' 5: Status
                GetType(String),         ' 6: Due date
                GetType(String),         ' 7: Description (hidden)
                GetType(String)          ' 8: Id (hidden)
            )
            
            pTreeView = New TreeView(pListStore)
            pTreeView.HeadersVisible = True
            pTreeView.EnableSearch = True
            pTreeView.SearchColumn = 2  ' Search by Title
            
            ' Priority column (icon) - FIXED: Use proper TreeViewColumn constructor
            Dim lPriorityColumn As New TreeViewColumn()
            lPriorityColumn.Title = "Pri"  ' Set Title property instead of passing to constructor
            Dim lPriorityRenderer As New CellRendererPixbuf()
            lPriorityColumn.PackStart(lPriorityRenderer, False)
            lPriorityColumn.AddAttribute(lPriorityRenderer, "pixbuf", 0)
            lPriorityColumn.SortColumnId = 3
            lPriorityColumn.Resizable = True
            lPriorityColumn.MinWidth = 40
            pTreeView.AppendColumn(lPriorityColumn)
            
            ' Category column (icon) - FIXED: Use proper TreeViewColumn constructor
            Dim lCategoryColumn As New TreeViewColumn()
            lCategoryColumn.Title = "Cat"  ' Set Title property instead of passing to constructor
            Dim lCategoryRenderer As New CellRendererPixbuf()
            lCategoryColumn.PackStart(lCategoryRenderer, False)
            lCategoryColumn.AddAttribute(lCategoryRenderer, "pixbuf", 1)
            lCategoryColumn.SortColumnId = 4
            lCategoryColumn.Resizable = True
            lCategoryColumn.MinWidth = 40
            pTreeView.AppendColumn(lCategoryColumn)
            
            ' Title column - FIXED: Use proper TreeViewColumn constructor
            Dim lTitleColumn As New TreeViewColumn()
            lTitleColumn.Title = "Title"
            Dim lTitleRenderer As New CellRendererText()
            lTitleColumn.PackStart(lTitleRenderer, True)
            lTitleColumn.AddAttribute(lTitleRenderer, "Text", 2)
            lTitleColumn.SortColumnId = 2
            lTitleColumn.Resizable = True
            lTitleColumn.Expand = True
            pTreeView.AppendColumn(lTitleColumn)
            
            ' Status column - FIXED: Use proper TreeViewColumn constructor
            Dim lStatusColumn As New TreeViewColumn()
            lStatusColumn.Title = "Status"
            Dim lStatusRenderer As New CellRendererText()
            lStatusColumn.PackStart(lStatusRenderer, False)
            lStatusColumn.AddAttribute(lStatusRenderer, "Text", 5)
            lStatusColumn.SortColumnId = 5
            lStatusColumn.Resizable = True
            lStatusColumn.MinWidth = 80
            pTreeView.AppendColumn(lStatusColumn)
            
            ' Due Date column - FIXED: Use proper TreeViewColumn constructor
            Dim lDueDateColumn As New TreeViewColumn()
            lDueDateColumn.Title = "Due Date"
            Dim lDueDateRenderer As New CellRendererText()
            lDueDateColumn.PackStart(lDueDateRenderer, False)
            lDueDateColumn.AddAttribute(lDueDateRenderer, "Text", 6)
            lDueDateColumn.SortColumnId = 6
            lDueDateColumn.Resizable = True
            lDueDateColumn.MinWidth = 100
            pTreeView.AppendColumn(lDueDateColumn)
            
            ' Connect events
            AddHandler pTreeView.Selection.Changed, AddressOf OnSelectionChanged
            AddHandler pTreeView.RowActivated, AddressOf OnRowActivated
            AddHandler pTreeView.ButtonReleaseEvent, AddressOf OnTreeViewButtonRelease
            
        End Sub
        
        Private Sub CreateContextMenu()
            Try
                pContextMenu = New Menu()
                
                ' Edit (for manual TODOs)
                Dim lEditItem As New MenuItem("Edit TODO")
                AddHandler lEditItem.Activated, AddressOf OnEditTODO
                pContextMenu.Append(lEditItem)
                
                ' Mark as completed (for manual TODOs)
                Dim lCompleteItem As New MenuItem("Mark as Completed")
                AddHandler lCompleteItem.Activated, AddressOf OnMarkCompleted
                pContextMenu.Append(lCompleteItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Send to AI
                Dim lSendToAIItem As New MenuItem("Send to AI Assistant")
                AddHandler lSendToAIItem.Activated, AddressOf OnSendToAI
                pContextMenu.Append(lSendToAIItem)
                
                ' Navigate to source (for code TODOs)
                Dim lNavigateItem As New MenuItem("Go to Source")
                AddHandler lNavigateItem.Activated, AddressOf OnNavigateToSource
                pContextMenu.Append(lNavigateItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Delete (for manual TODOs)
                Dim lDeleteItem As New MenuItem("Delete TODO")
                AddHandler lDeleteItem.Activated, AddressOf OnDeleteTODO
                pContextMenu.Append(lDeleteItem)
                
                pContextMenu.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"error creating Context menu: {ex.Message}")
            End Try
        End Sub

        Private Function CompareTitles(vModel As ITreeModel, vA As TreeIter, vB As TreeIter) As Integer
            Try
                Dim lTitleA As String = CStr(vModel.GetValue(vA, 0))
                Dim lTitleB As String = CStr(vModel.GetValue(vB, 0))
                Return String.Compare(lTitleA, lTitleB, StringComparison.OrdinalIgnoreCase)
            Catch ex As Exception
                Return 0
            End Try
        End Function
        
        Private Function ComparePriorities(vModel As ITreeModel, vA As TreeIter, vB As TreeIter) As Integer
            Try
                ' Stored as string, need to parse back to enum
                Dim lPriorityA As String = CStr(vModel.GetValue(vA, 5))
                Dim lPriorityB As String = CStr(vModel.GetValue(vB, 5))
                
                ' Parse priority values (stored as "eLow", "eMedium", etc.)
                Dim lEnumA As TODOItem.ePriority = ParsePriorityString(lPriorityA)
                Dim lEnumB As TODOItem.ePriority = ParsePriorityString(lPriorityB)
                
                ' Higher priority should come first (reverse order)
                Return CInt(lEnumB) - CInt(lEnumA)
            Catch ex As Exception
                Return 0
            End Try
        End Function
        
        Private Function CompareCategories(vModel As ITreeModel, vA As TreeIter, vB As TreeIter) As Integer
            Try
                Dim lCategoryA As String = CStr(vModel.GetValue(vA, 2))
                Dim lCategoryB As String = CStr(vModel.GetValue(vB, 2))
                Return String.Compare(lCategoryA, lCategoryB, StringComparison.OrdinalIgnoreCase)
            Catch ex As Exception
                Return 0
            End Try
        End Function
        
        Private Function CompareStatuses(vModel As ITreeModel, vA As TreeIter, vB As TreeIter) As Integer
            Try
                Dim lStatusA As String = CStr(vModel.GetValue(vA, 3))
                Dim lStatusB As String = CStr(vModel.GetValue(vB, 3))
                Return String.Compare(lStatusA, lStatusB, StringComparison.OrdinalIgnoreCase)
            Catch ex As Exception
                Return 0
            End Try
        End Function
        
        Private Function ParsePriorityString(vPriorityStr As String) As TODOItem.ePriority
            Select Case vPriorityStr
                Case "eLow", "Low"
                    Return TODOItem.ePriority.eLow
                Case "eMedium", "Medium"
                    Return TODOItem.ePriority.eMedium
                Case "eHigh", "High"
                    Return TODOItem.ePriority.eHigh
                Case "eCritical", "Critical"
                    Return TODOItem.ePriority.eCritical
                Case Else
                    Return TODOItem.ePriority.eUnspecified
            End Select
        End Function

        Protected Overrides Sub OnShown()
            MyBase.OnShown()
            Console.WriteLine($"TODO OnShown")
        End Sub
        
        Private Sub ConnectEvents()
            Try
                ' Connect toolbar events
                AddHandler pAddButton.Clicked, AddressOf OnAddTODO
                AddHandler pRefreshButton.Clicked, AddressOf OnRefreshTODOs
                
                ' Connect filter events
                AddHandler pSearchEntry.Changed, AddressOf OnFilterChanged
                AddHandler pPriorityCombo.Changed, AddressOf OnFilterChanged
                AddHandler pCategoryCombo.Changed, AddressOf OnFilterChanged
                AddHandler pStatusCombo.Changed, AddressOf OnFilterChanged
                AddHandler pOverdueToggle.Toggled, AddressOf OnFilterChanged
                
                ' Connect tree view events
                AddHandler pTreeView.RowActivated, AddressOf OnRowActivated
                AddHandler pTreeView.ButtonReleaseEvent, AddressOf OnTreeViewButtonRelease
                
                ' Connect selection change
                Dim lSelection As TreeSelection = pTreeView.Selection
                AddHandler lSelection.Changed, AddressOf OnSelectionChanged
                
                ' Set initial sort
                pListStore.SetSortFunc(0, AddressOf CompareTitles)
                pListStore.SetSortFunc(1, AddressOf ComparePriorities)
                pListStore.SetSortFunc(2, AddressOf CompareCategories)
                pListStore.SetSortFunc(3, AddressOf CompareStatuses)
     
                ' Apply initial sort
                pListStore.SetSortColumnId(1, SortType.Descending) ' Sort by Priority by default
        
                
            Catch ex As Exception
                Console.WriteLine($"error connecting events: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnRowActivated(vSender As Object, vE As RowActivatedArgs)
            Try
                Dim lIter As TreeIter
                If pListStore.GetIter(lIter, vE.Path) Then
                    Dim lId As String = CStr(pListStore.GetValue(lIter, 8))
                    
                    If Not String.IsNullOrEmpty(lId) Then
                        pSelectedTODO = pAllTODOs.FirstOrDefault(Function(t) t.Id = lId)
                        If pSelectedTODO IsNot Nothing Then
                            RaiseEvent TODODoubleClicked(pSelectedTODO)
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error handling row activation: {ex.Message}")
            End Try
        End Sub
        
        Private Function OnTreeViewButtonRelease(vSender As Object, vE As ButtonReleaseEventArgs) As Boolean
            Try
                If vE.Event.Button = 3 Then ' Right click
                    Dim lPath As TreePath = Nothing
                    
                    If pTreeView.GetPathAtPos(CInt(vE.Event.x), CInt(vE.Event.y), lPath) Then
                        ' CRITICAL: Grab focus first - this fixes the double-click issue
                        If Not pTreeView.HasFocus Then
                            pTreeView.GrabFocus()
                        End If
                        
                        ' Set cursor to clicked item
                        pTreeView.SetCursor(lPath, Nothing, False)
                        
                        ' Update menu sensitivity - this is a private method in the same class
                        UpdateContextMenuForTODO()
                        
                        ' Show menu
                        pContextMenu.ShowAll()
                        pContextMenu.PopupAtPointer(vE.Event)
                    End If
                    
                    Return True
                End If
            Catch ex As Exception
                Console.WriteLine($"error handling tree view button press: {ex.Message}")
            End Try
            
            Return False
        End Function
        
        Private Sub UpdateContextMenuForTODO()
            Try
                If pSelectedTODO Is Nothing Then Return
                
                ' Get menu items (assuming order matches creation)
                Dim lItems As Widget() = pContextMenu.Children
                If lItems.Length >= 6 Then
                    ' Edit - only for manual TODOs
                    lItems(0).Sensitive = CBool(pSelectedTODO.SourceType = TODOItem.eSourceType.eManual)
                    
                    ' Mark as completed - only for manual TODOs that aren't already completed
                    lItems(1).Sensitive = CBool(pSelectedTODO.SourceType = TODOItem.eSourceType.eManual AndAlso 
                                         pSelectedTODO.Status <> TODOItem.eStatus.eCompleted)
                    
                    ' Navigate to source - only for code TODOs
                    lItems(4).Sensitive = CBool(pSelectedTODO.SourceType = TODOItem.eSourceType.eCodeComment)
                    
                    ' Delete - only for manual TODOs
                    lItems(6).Sensitive = CBool(pSelectedTODO.SourceType = TODOItem.eSourceType.eManual)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error updating Context menu: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnSelectionChanged(vSender As Object, vE As EventArgs)
            Try
                Dim lSelection As TreeSelection = CType(vSender, TreeSelection)
                Dim lIter As TreeIter
                
                If lSelection.GetSelected(lIter) Then
                    Dim lId As String = CStr(pListStore.GetValue(lIter, 8))
                    
                    If Not String.IsNullOrEmpty(lId) Then
                        pSelectedTODO = pAllTODOs.FirstOrDefault(Function(t) t.Id = lId)
                        If pSelectedTODO IsNot Nothing Then
                            RaiseEvent TodoSelected(pSelectedTODO)
                        End If
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error handling selection change: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnAddTODO(vSender As Object, vE As EventArgs)
            Try
                Dim lDialog As New TODOEditDialog(Nothing, "Add TODO")
                lDialog.TransientFor = CType(Me.Toplevel, Window)
                
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    If pTODOManager IsNot Nothing Then
                        pTODOManager.AddTODO(lDialog.TODOTitle, lDialog.TODODescription, lDialog.TODOPriority, lDialog.TODOCategory)
                        RefreshTODOs()
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error adding TODO: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnEditTODO(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO Is Nothing OrElse pSelectedTODO.SourceType <> TODOItem.eSourceType.eManual Then
                    Return
                End If
                
                Dim lDialog As New TODOEditDialog(pSelectedTODO, "Edit TODO")
                lDialog.TransientFor = CType(Me.Toplevel, Window)
                
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    ' Update the TODO
                    pSelectedTODO.Title = lDialog.TODOTitle
                    pSelectedTODO.Description = lDialog.TODODescription
                    pSelectedTODO.Priority = lDialog.TODOPriority
                    pSelectedTODO.Category = lDialog.TODOCategory
                    pSelectedTODO.Status = lDialog.TODOStatus
                    pSelectedTODO.DueDate = lDialog.TODODueDate
                    pSelectedTODO.Progress = lDialog.TODOProgress
                    
                    If pTODOManager IsNot Nothing Then
                        pTODOManager.UpdateTODO(pSelectedTODO)
                        RefreshTODOs()
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error editing TODO: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnMarkCompleted(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO Is Nothing OrElse pSelectedTODO.SourceType <> TODOItem.eSourceType.eManual Then
                    Return
                End If
                
                pSelectedTODO.Status = TODOItem.eStatus.eCompleted
                pSelectedTODO.CompletedDate = DateTime.Now
                pSelectedTODO.Progress = 100
                
                If pTODOManager IsNot Nothing Then
                    pTODOManager.UpdateTODO(pSelectedTODO)
                    RefreshTODOs()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error marking TODO completed: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnSendToAI(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO IsNot Nothing Then
                    RaiseEvent SendToAI(pSelectedTODO)
                End If
            Catch ex As Exception
                Console.WriteLine($"error sending TODO to AI: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnNavigateToSource(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO IsNot Nothing AndAlso pSelectedTODO.SourceType = TODOItem.eSourceType.eCodeComment Then
                    RaiseEvent NavigateToCode(pSelectedTODO.SourceFile, pSelectedTODO.SourceLine, pSelectedTODO.SourceColumn)
                End If
            Catch ex As Exception
                Console.WriteLine($"error navigating to source: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnDeleteTODO(vSender As Object, vE As EventArgs)
            Try
                If pSelectedTODO Is Nothing OrElse pSelectedTODO.SourceType <> TODOItem.eSourceType.eManual Then
                    Return
                End If
                
                Dim lDialog As New MessageDialog(
                    CType(Me.Toplevel, Window),
                    DialogFlags.Modal,
                    MessageType.Question,
                    ButtonsType.YesNo,
                    $"Are you sure you want to Delete the TODO '{pSelectedTODO.Title}'?"
                )
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    If pTODOManager IsNot Nothing Then
                        pTODOManager.DeleteTODO(pSelectedTODO.Id)
                        RefreshTODOs()
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error deleting TODO: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnRefreshTODOs(vSender As Object, vE As EventArgs)
            RefreshTODOs()
        End Sub
        
        Private Sub OnFilterChanged(vSender As Object, vE As EventArgs)
            ApplyFilters()
        End Sub
        
        ' Then update the SetProjectRoot method:
        Public Sub SetProjectRoot(vProjectRoot As String)
            Try
                ' Create TODO manager for this project
                If Not String.IsNullOrEmpty(vProjectRoot) Then
                    pTODOManager = New TODOManager(vProjectRoot)
                    Console.WriteLine("TODOManager New()")
                    AddHandler pTODOManager.TODOsChanged, AddressOf OnTODOsChanged
                    
                    ' IMPORTANT: Refresh TODOs after setting project root
                    RefreshTODOs()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error setting project root: {ex.Message}")
            End Try
        End Sub
            
        Private Sub OnTODOsChanged()
            GLib.Idle.Add(Function()
                RefreshTODOs()
                Return False
            End Function)
        End Sub
        
        ' Updated RefreshTODOs method for TODOPanel.vb
        Friend Sub RefreshTODOs()
            Try
                Console.WriteLine($"RefreshTODOs Started")
                If pTODOManager Is Nothing Then
                    Console.WriteLine("TODO Manager not initialized")
                    Return
                End If
                
                ' Clear existing items
                pListStore.Clear()
                
                ' Load TODOs
                Dim lTODOs As List(Of TODOItem) = pTODOManager.LoadTODOs()
                Console.WriteLine($"loaded {lTODOs.Count} TODO items")
                
                ' Store all TODOs for filtering
                pAllTODOs = lTODOs
                pFilteredTODOs = lTODOs
                
                ' Populate all TODOs
                For Each lTODO In lTODOs
                    PopulateListItem(lTODO)
                Next
                
                ' Update status
                UpdateStatusBar()
                Console.WriteLine($"RefreshTODOs Finished")
                
            Catch ex As Exception
                Console.WriteLine($"error refreshing TODOs: {ex.Message}")
            End Try
        End Sub
        
        ' Updated PopulateListItem method - this should match the ListStore structure
        Private Sub PopulateListItem(vTODO As TODOItem)
            Try
                ' Create icons for priority and category
                Dim lPriorityIcon As Gdk.Pixbuf = CreatePriorityIcon(vTODO.Priority)
                Dim lCategoryIcon As Gdk.Pixbuf = CreateCategoryIcon(vTODO.Category)
                Dim lDueDateText As String = If(vTODO.DueDate.HasValue, vTODO.DueDate.Value.ToString("yyyy-MM-dd"), "")
                
                ' Append values matching the ListStore structure:
                ' 0: Priority icon (Pixbuf)
                ' 1: Category icon (Pixbuf)
                ' 2: Title (String)
                ' 3: Priority text (String)
                ' 4: Category text (String)
                ' 5: Status (String)
                ' 6: Due date (String)
                ' 7: Description (String) - hidden
                ' 8: ID (String) - hidden
                pListStore.AppendValues(
                    lPriorityIcon,                        ' 0: Priority Icon
                    lCategoryIcon,                        ' 1: Category Icon
                    vTODO.GetDisplayTitle(),              ' 2: Title
                    vTODO.GetPriorityDisplayText(),       ' 3: Priority Text
                    vTODO.GetCategoryDisplayText(),       ' 4: Category Text
                    vTODO.GetStatusDisplayText(),         ' 5: Status
                    lDueDateText,                         ' 6: Due date
                    vTODO.GetFormattedDescription(),      ' 7: Description
                    vTODO.Id                              ' 8: Id
                )
                
            Catch ex As Exception
                Console.WriteLine($"error populating TODO item: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ApplyFilters()
            Try
                If pTODOManager Is Nothing Then
                    Return
                End If
                
                Dim lSearchText As String = pSearchEntry.Text
                Dim lPriorityFilter As TODOItem.ePriority? = GetSelectedPriority()
                Dim lCategoryFilter As TODOItem.eCategory? = GetSelectedCategory()
                Dim lStatusFilter As TODOItem.eStatus? = GetSelectedStatus()
                Dim lShowOverdueOnly As Boolean = pOverdueToggle.Active
                
                pFilteredTODOs = pTODOManager.FilterTODOs(pAllTODOs, lSearchText, lPriorityFilter, lCategoryFilter, lStatusFilter, lShowOverdueOnly)
                
                UpdateTreeView()
                UpdateStatusBar()
                
            Catch ex As Exception
                Console.WriteLine($"error applying filters: {ex.Message}")
            End Try
        End Sub
        
        Private Function GetSelectedPriority() As TODOItem.ePriority?
            Select Case pPriorityCombo.Active
                Case 1
                    Return TODOItem.ePriority.eCritical
                Case 2
                    Return TODOItem.ePriority.eHigh
                Case 3
                    Return TODOItem.ePriority.eMedium
                Case 4
                    Return TODOItem.ePriority.eLow
                Case Else
                    Return Nothing
            End Select
        End Function
        
        Private Function GetSelectedCategory() As TODOItem.eCategory?
            Select Case pCategoryCombo.Active
                Case 1
                    Return TODOItem.eCategory.eBug
                Case 2
                    Return TODOItem.eCategory.eFeature
                Case 3
                    Return TODOItem.eCategory.eDocumentation
                Case 4
                    Return TODOItem.eCategory.eRefactor
                Case 5
                    Return TODOItem.eCategory.eTesting
                Case 6
                    Return TODOItem.eCategory.ePerformance
                Case 7
                    Return TODOItem.eCategory.eSecurity
                Case 8
                    Return TODOItem.eCategory.eUI
                Case 9
                    Return TODOItem.eCategory.eOther
                Case Else
                    Return Nothing
            End Select
        End Function
        
        Private Function GetSelectedStatus() As TODOItem.eStatus?
            Select Case pStatusCombo.Active
                Case 1
                    Return TODOItem.eStatus.ePending
                Case 2
                    Return TODOItem.eStatus.eInProgress
                Case 3
                    Return TODOItem.eStatus.eCompleted
                Case 4
                    Return TODOItem.eStatus.eCancelled
                Case Else
                    Return Nothing
            End Select
        End Function
        
        Private Sub UpdateTreeView()
            Try
                pListStore.Clear()
                
                ' Show empty state if no TODOs or no manager
                If pTODOManager Is Nothing OrElse pFilteredTODOs.Count = 0 Then
                    ShowEmptyState()
                    Return
                End If
                
                For Each lTODO In pFilteredTODOs
                    Dim lPriorityIcon As Gdk.Pixbuf = CreatePriorityIcon(lTODO.Priority)
                    Dim lCategoryIcon As Gdk.Pixbuf = CreateCategoryIcon(lTODO.Category)
                    Dim lDueDateText As String = If(lTODO.DueDate.HasValue, lTODO.DueDate.Value.ToString("yyyy-MM-dd"), "")
                    
                    pListStore.AppendValues(
                        lPriorityIcon,
                        lCategoryIcon,
                        lTODO.GetDisplayTitle(),
                        lTODO.GetPriorityDisplayText(),
                        lTODO.GetCategoryDisplayText(),
                        lTODO.GetStatusDisplayText(),
                        lDueDateText,
                        lTODO.GetFormattedDescription(),
                        lTODO.Id
                    )
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error updating tree view: {ex.Message}")
            End Try
        End Sub
        
        Private Function CreatePriorityIcon(vPriority As TODOItem.ePriority) As Gdk.Pixbuf
            Try
                Dim lIconName As String = "dialog-information-symbolic"
                
                Select Case vPriority
                    Case TODOItem.ePriority.eLow
                        lIconName = "dialog-information-symbolic"
                    Case TODOItem.ePriority.eMedium
                        lIconName = "dialog-warning-symbolic"
                    Case TODOItem.ePriority.eHigh
                        lIconName = "dialog-error-symbolic"
                    Case TODOItem.ePriority.eCritical
                        lIconName = "security-high-symbolic"
                End Select
                
                Dim lIconTheme As Gtk.IconTheme = Gtk.IconTheme.Default
                Return lIconTheme.LoadIcon(lIconName, 16, IconLookupFlags.UseBuiltin)
                
            Catch ex As Exception
                Console.WriteLine($"error creating Priority Icon: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function CreateCategoryIcon(vCategory As TODOItem.eCategory) As Gdk.Pixbuf
            Try
                Dim lIconName As String = GetCategoryIconName(vCategory)
                Dim lIconTheme As Gtk.IconTheme = Gtk.IconTheme.Default
                Return lIconTheme.LoadIcon(lIconName, 16, IconLookupFlags.UseBuiltin)
                
            Catch ex As Exception
                Console.WriteLine($"error creating Category Icon: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Private Function GetCategoryIconName(vCategory As TODOItem.eCategory) As String
            Select Case vCategory
                Case TODOItem.eCategory.eBug
                    Return "bug-symbolic"
                Case TODOItem.eCategory.eFeature
                    Return "starred-symbolic"
                Case TODOItem.eCategory.eDocumentation
                    Return "help-browser-symbolic"
                Case TODOItem.eCategory.eRefactor
                    Return "preferences-system-symbolic"
                Case TODOItem.eCategory.eTesting
                    Return "checkbox-symbolic"
                Case TODOItem.eCategory.ePerformance
                    Return "applications-utilities-symbolic"
                Case TODOItem.eCategory.eSecurity
                    Return "security-high-symbolic"
                Case TODOItem.eCategory.eUI
                    Return "applications-graphics-symbolic"
                Case Else
                    Return "Text-x-generic-symbolic"
            End Select
        End Function
        
        Private Sub UpdateStatusBar()
            Try
                If pStatusBar Is Nothing Then pStatusBar = New Label

                If pAllTODOs Is Nothing OrElse pFilteredTODOs Is Nothing Then
                    pStatusBar.Text = "Ready"
                    Return
                End If
                
                If pTODOManager IsNot Nothing Then
                    Dim lStats = pTODOManager.GetTODOStatistics(pAllTODOs)
                    Dim lFilteredCount = pFilteredTODOs.Count
                    Dim lTotalCount = lStats("Total")
                    
                    Dim lStatusText As String = $"Showing {lFilteredCount} of {lTotalCount} TODOs"
                    
                    If lStats("Overdue") > 0 Then
                        lStatusText &= $" | {lStats("Overdue")} Overdue"
                    End If
                    
                    If lStats("Critical") > 0 Then
                        lStatusText &= $" | {lStats("Critical")} Critical"
                    End If
                    
                    pStatusBar.Text = lStatusText
                Else
                    Console.WriteLine($"No project loaded in TodoPanel.UpdateStatusBar")
                    pStatusBar.Text = "No project loaded"
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error updating Status bar: {ex.Message}")
                pStatusBar.Text = "error"
            End Try
        End Sub
        
        Private Sub ShowEmptyState()
            Try
                pListStore.Clear()
                
                ' Add a helpful message row
                Dim lMessage As String = If(pTODOManager Is Nothing, 
                    "No project loaded - Open a project to view TODOs",
                    "No TODOs found - Click Add to create a New TODO")
                
                ' Use empty pixbufs for icons
                pListStore.AppendValues(
                    Nothing,  ' Priority Icon
                    Nothing,  ' Category Icon
                    lMessage, ' Title
                    "",       ' Priority Text
                    "",       ' Category Text
                    "",       ' Status
                    "",       ' Due date
                    "",       ' Description
                    ""        ' Id
                )
                
            Catch ex As Exception
                Console.WriteLine($"error showing empty state: {ex.Message}")
            End Try
        End Sub
        
        ' Comparison functions for sorting
        Private Function ComparePriority(vModel As ITreeModel, vA As TreeIter, vB As TreeIter) As Integer
            Try
                Dim lPriorityA As String = CStr(vModel.GetValue(vA, 3))
                Dim lPriorityB As String = CStr(vModel.GetValue(vB, 3))
                
                Dim lPriorityValueA As Integer = GetPriorityValue(lPriorityA)
                Dim lPriorityValueB As Integer = GetPriorityValue(lPriorityB)
                
                Return lPriorityValueA.CompareTo(lPriorityValueB)
            Catch
                Return 0
            End Try
        End Function
        
        Private Function GetPriorityValue(vPriority As String) As Integer
            Select Case vPriority
                Case "Critical"
                    Return 4
                Case "High"
                    Return 3
                Case "Medium"
                    Return 2
                Case "Low"
                    Return 1
                Case Else
                    Return 0
            End Select
        End Function
        
        Private Function CompareCategory(vModel As ITreeModel, vA As TreeIter, vB As TreeIter) As Integer
            Try
                Dim lCategoryA As String = CStr(vModel.GetValue(vA, 4))
                Dim lCategoryB As String = CStr(vModel.GetValue(vB, 4))
                Return String.Compare(lCategoryA, lCategoryB)
            Catch
                Return 0
            End Try
        End Function
        
        Private Function CompareStatus(vModel As ITreeModel, vA As TreeIter, vB As TreeIter) As Integer
            Try
                Dim lStatusA As String = CStr(vModel.GetValue(vA, 5))
                Dim lStatusB As String = CStr(vModel.GetValue(vB, 5))
                Return String.Compare(lStatusA, lStatusB)
            Catch
                Return 0
            End Try
        End Function
        
        Private Function CompareDueDate(vModel As ITreeModel, vA As TreeIter, vB As TreeIter) As Integer
            Try
                Dim lDateA As String = CStr(vModel.GetValue(vA, 6))
                Dim lDateB As String = CStr(vModel.GetValue(vB, 6))
                
                If String.IsNullOrEmpty(lDateA) AndAlso String.IsNullOrEmpty(lDateB) Then
                    Return 0
                ElseIf String.IsNullOrEmpty(lDateA) Then
                    Return 1
                ElseIf String.IsNullOrEmpty(lDateB) Then
                    Return -1
                Else
                    Return String.Compare(lDateA, lDateB)
                End If
            Catch
                Return 0
            End Try
        End Function
        
    End Class
    
    ' TODO Edit Dialog
    Public Class TODOEditDialog
        Inherits Dialog
        
        Private pTitleEntry As Entry
        Private pDescriptionTextView As TextView
        Private pPriorityCombo As ComboBoxText
        Private pCategoryCombo As ComboBoxText
        Private pStatusCombo As ComboBoxText
        Private pDueDateCalendar As Calendar
        Private pDueDateCheckButton As CheckButton
        Private pProgressScale As Scale
        Private pTagsEntry As Entry
        
        Public ReadOnly Property TODOTitle As String
            Get
                Return pTitleEntry.Text
            End Get
        End Property
        
        Public ReadOnly Property TODODescription As String
            Get
                Return pDescriptionTextView.Buffer.Text
            End Get
        End Property
        
        Public ReadOnly Property TODOPriority As TODOItem.ePriority
            Get
                Select Case pPriorityCombo.Active
                    Case 0
                        Return TODOItem.ePriority.eLow
                    Case 1
                        Return TODOItem.ePriority.eMedium
                    Case 2
                        Return TODOItem.ePriority.eHigh
                    Case 3
                        Return TODOItem.ePriority.eCritical
                    Case Else
                        Return TODOItem.ePriority.eMedium
                End Select
            End Get
        End Property
        
        Public ReadOnly Property TODOCategory As TODOItem.eCategory
            Get
                Select Case pCategoryCombo.Active
                    Case 0
                        Return TODOItem.eCategory.eBug
                    Case 1
                        Return TODOItem.eCategory.eFeature
                    Case 2
                        Return TODOItem.eCategory.eDocumentation
                    Case 3
                        Return TODOItem.eCategory.eRefactor
                    Case 4
                        Return TODOItem.eCategory.eTesting
                    Case 5
                        Return TODOItem.eCategory.ePerformance
                    Case 6
                        Return TODOItem.eCategory.eSecurity
                    Case 7
                        Return TODOItem.eCategory.eUI
                    Case 8
                        Return TODOItem.eCategory.eOther
                    Case Else
                        Return TODOItem.eCategory.eOther
                End Select
            End Get
        End Property
        
        Public ReadOnly Property TODOStatus As TODOItem.eStatus
            Get
                Select Case pStatusCombo.Active
                    Case 0
                        Return TODOItem.eStatus.ePending
                    Case 1
                        Return TODOItem.eStatus.eInProgress
                    Case 2
                        Return TODOItem.eStatus.eCompleted
                    Case 3
                        Return TODOItem.eStatus.eCancelled
                    Case Else
                        Return TODOItem.eStatus.ePending
                End Select
            End Get
        End Property
        
        Public ReadOnly Property TODODueDate As DateTime?
            Get
                If pDueDateCheckButton.Active Then
                    Dim lYear As UInteger
                    Dim lMonth As UInteger
                    Dim lDay As UInteger
                    pDueDateCalendar.GetDate(lYear, lMonth, lDay)
                    Return New DateTime(CInt(lYear), CInt(lMonth) + 1, CInt(lDay))
                Else
                    Return Nothing
                End If
            End Get
        End Property
        
        Public ReadOnly Property TODOProgress As Integer
            Get
                Return CInt(pProgressScale.Value)
            End Get
        End Property
        
        Public ReadOnly Property TODOTags As List(Of String)
            Get
                Dim lTags As New List(Of String)
                If Not String.IsNullOrEmpty(pTagsEntry.Text) Then
                    lTags.AddRange(pTagsEntry.Text.Split(","c).Select(Function(t) t.Trim()).Where(Function(t) Not String.IsNullOrEmpty(t)))
                End If
                Return lTags
            End Get
        End Property
        
        Public Sub New(vTODO As TODOItem, vTitle As String)
            MyBase.New(vTitle, Nothing, DialogFlags.Modal)
            
            SetDefaultSize(500, 600)
            BorderWidth = 10
            
            BuildUI()
            
            ' Populate if editing
            If vTODO IsNot Nothing Then
                PopulateFields(vTODO)
            End If
            
            ' Add buttons
            AddButton("Cancel", ResponseType.Cancel)
            AddButton("OK", ResponseType.Ok)
            
            ShowAll()
        End Sub
        
        Private Sub BuildUI()
            Try
                Dim lVBox As New Box(Orientation.Vertical, 10)
                
                ' Title
                Dim lTitleFrame As New Frame("Title")
                pTitleEntry = New Entry()
                lTitleFrame.Add(pTitleEntry)
                lVBox.PackStart(lTitleFrame, False, False, 0)
                
                ' Description
                Dim lDescFrame As New Frame("Description")
                pDescriptionTextView = New TextView()
                pDescriptionTextView.WrapMode = WrapMode.Word
                Dim lScrolled As New ScrolledWindow()
                lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                lScrolled.SetSizeRequest(-1, 150)
                lScrolled.Add(pDescriptionTextView)
                lDescFrame.Add(lScrolled)
                lVBox.PackStart(lDescFrame, True, True, 0)
                
                ' Priority and Category
                Dim lPriCatBox As New Box(Orientation.Horizontal, 10)
                
                Dim lPriorityFrame As New Frame("Priority")
                pPriorityCombo = New ComboBoxText()
                pPriorityCombo.AppendText("Low")
                pPriorityCombo.AppendText("Medium")
                pPriorityCombo.AppendText("High")
                pPriorityCombo.AppendText("Critical")
                pPriorityCombo.Active = 1 ' Default to Medium
                lPriorityFrame.Add(pPriorityCombo)
                lPriCatBox.PackStart(lPriorityFrame, True, True, 0)
                
                Dim lCategoryFrame As New Frame("Category")
                pCategoryCombo = New ComboBoxText()
                pCategoryCombo.AppendText("Bug")
                pCategoryCombo.AppendText("Feature")
                pCategoryCombo.AppendText("documentation")
                pCategoryCombo.AppendText("Refactor")
                pCategoryCombo.AppendText("Testing")
                pCategoryCombo.AppendText("Performance")
                pCategoryCombo.AppendText("Security")
                pCategoryCombo.AppendText("UI/UX")
                pCategoryCombo.AppendText("Other")
                pCategoryCombo.Active = 8 ' Default to Other
                lCategoryFrame.Add(pCategoryCombo)
                lPriCatBox.PackStart(lCategoryFrame, True, True, 0)
                
                lVBox.PackStart(lPriCatBox, False, False, 0)
                
                ' Status and Progress
                Dim lStatusProgressBox As New Box(Orientation.Horizontal, 10)
                
                Dim lStatusFrame As New Frame("Status")
                pStatusCombo = New ComboBoxText()
                pStatusCombo.AppendText("Pending")
                pStatusCombo.AppendText("in Progress")
                pStatusCombo.AppendText("Completed")
                pStatusCombo.AppendText("Cancelled")
                pStatusCombo.Active = 0 ' Default to Pending
                lStatusFrame.Add(pStatusCombo)
                lStatusProgressBox.PackStart(lStatusFrame, True, True, 0)
                
                Dim lProgressFrame As New Frame("Progress %")
                pProgressScale = New Scale(Orientation.Horizontal, 0, 100, 5)
                pProgressScale.DrawValue = True
                pProgressScale.ValuePos = PositionType.Right
                lProgressFrame.Add(pProgressScale)
                lStatusProgressBox.PackStart(lProgressFrame, True, True, 0)
                
                lVBox.PackStart(lStatusProgressBox, False, False, 0)
                
                ' Due Date
                Dim lDueDateFrame As New Frame("Due Date")
                Dim lDueDateBox As New Box(Orientation.Vertical, 5)
                pDueDateCheckButton = New CheckButton("Set due date")
                AddHandler pDueDateCheckButton.Toggled, AddressOf OnDueDateToggled
                lDueDateBox.PackStart(pDueDateCheckButton, False, False, 0)
                
                pDueDateCalendar = New Calendar()
                pDueDateCalendar.Sensitive = False
                lDueDateBox.PackStart(pDueDateCalendar, False, False, 0)
                lDueDateFrame.Add(lDueDateBox)
                lVBox.PackStart(lDueDateFrame, False, False, 0)
                
                ' Tags
                Dim lTagsFrame As New Frame("Tags (comma-separated)")
                pTagsEntry = New Entry()
                pTagsEntry.PlaceholderText = "e.g. urgent, client, backend"
                lTagsFrame.Add(pTagsEntry)
                lVBox.PackStart(lTagsFrame, False, False, 0)
                
                ' Add to dialog content area
                ContentArea.Add(lVBox)
                
            Catch ex As Exception
                Console.WriteLine($"error building TODO dialog UI: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnDueDateToggled(vSender As Object, vE As EventArgs)
            pDueDateCalendar.Sensitive = pDueDateCheckButton.Active
        End Sub
        
        Private Sub PopulateFields(vTODO As TODOItem)
            Try
                pTitleEntry.Text = vTODO.Title
                pDescriptionTextView.Buffer.Text = vTODO.Description
                
                ' Set priority
                Select Case vTODO.Priority
                    Case TODOItem.ePriority.eLow
                        pPriorityCombo.Active = 0
                    Case TODOItem.ePriority.eMedium
                        pPriorityCombo.Active = 1
                    Case TODOItem.ePriority.eHigh
                        pPriorityCombo.Active = 2
                    Case TODOItem.ePriority.eCritical
                        pPriorityCombo.Active = 3
                End Select
                
                ' Set category
                Select Case vTODO.Category
                    Case TODOItem.eCategory.eBug
                        pCategoryCombo.Active = 0
                    Case TODOItem.eCategory.eFeature
                        pCategoryCombo.Active = 1
                    Case TODOItem.eCategory.eDocumentation
                        pCategoryCombo.Active = 2
                    Case TODOItem.eCategory.eRefactor
                        pCategoryCombo.Active = 3
                    Case TODOItem.eCategory.eTesting
                        pCategoryCombo.Active = 4
                    Case TODOItem.eCategory.ePerformance
                        pCategoryCombo.Active = 5
                    Case TODOItem.eCategory.eSecurity
                        pCategoryCombo.Active = 6
                    Case TODOItem.eCategory.eUI
                        pCategoryCombo.Active = 7
                    Case TODOItem.eCategory.eOther
                        pCategoryCombo.Active = 8
                End Select
                
                ' Set status
                Select Case vTODO.Status
                    Case TODOItem.eStatus.ePending
                        pStatusCombo.Active = 0
                    Case TODOItem.eStatus.eInProgress
                        pStatusCombo.Active = 1
                    Case TODOItem.eStatus.eCompleted
                        pStatusCombo.Active = 2
                    Case TODOItem.eStatus.eCancelled
                        pStatusCombo.Active = 3
                End Select
                
                ' Set progress
                pProgressScale.Value = vTODO.Progress
                
                ' Set due date
                If vTODO.DueDate.HasValue Then
                    pDueDateCheckButton.Active = True
                    pDueDateCalendar.Sensitive = True
                    pDueDateCalendar.SelectMonth(CUInt(vTODO.DueDate.Value.Month - 1), CUInt(vTODO.DueDate.Value.Year))
                    pDueDateCalendar.SelectDay(CUInt(vTODO.DueDate.Value.Day))
                End If
                
                ' Set tags
                If vTODO.Tags.Count > 0 Then
                    pTagsEntry.Text = String.Join(", ", vTODO.Tags)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error populating TODO fields: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
