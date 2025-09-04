' MainWindow.ObjectExplorerIntegration.vb - Complete Object Explorer integration
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Widgets
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax
Imports SimpleIDE.Utilities

Partial Public Class MainWindow
    Inherits Window
    
    ' ===== Object Explorer Integration Methods =====
    
    ' Object Explorer related fields
    'Private pObjectExplorer As ObjectExplorer
    Private pObjectExplorerScrolled As ScrolledWindow

    ' Store reference to root node for Object Explorer
    Private pRootNode As SyntaxNode

    ' Replace: SimpleIDE.MainWindow.SetupObjectExplorerForEditor
    ''' <summary>
    ''' Set up Object Explorer with current active editor
    ''' </summary>
    Private Sub SetupObjectExplorerForEditor(vEditor As IEditor)
        Try
            If pObjectExplorer Is Nothing OrElse vEditor Is Nothing Then Return
            
            ' Set the current editor in Object Explorer
            pObjectExplorer.SetCurrentEditor(vEditor)
            
            ' Hook up the DocumentParsed event if not already connected
            RemoveHandler vEditor.DocumentParsed, AddressOf OnEditorDocumentParsed
            AddHandler vEditor.DocumentParsed, AddressOf OnEditorDocumentParsed
            
            ' Get initial document structure if available
            Dim lStructure As SyntaxNode = vEditor.GetDocumentStructure()
            If lStructure IsNot Nothing Then
                pObjectExplorer.UpdateStructure(lStructure)
                
                ' ADDED: Also update navigation dropdowns if this is the current tab
                Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
                If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor Is vEditor Then
                    Console.WriteLine("SetupObjectExplorerForEditor: Initial structure available, updating navigation dropdowns")
                    UpdateNavigationDropdowns()
                End If
            Else
                ' ADDED: Schedule a parse if no structure available yet
                If TypeOf vEditor Is CustomDrawingEditor Then
                    Dim lCustomEditor As CustomDrawingEditor = DirectCast(vEditor, CustomDrawingEditor)
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"SetupObjectExplorerForEditor error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Enhanced document parsed handler with navigation dropdown support
    ''' </summary>
    ''' <param name="vRootNode">The root node of the parsed syntax tree</param>
    ''' <remarks>
    ''' This method updates both the Object Explorer and navigation dropdowns
    ''' when a document is parsed. The signature matches IEditor.DocumentParsedEventHandler.
    ''' </remarks>
    Private Sub OnEditorDocumentParsed(vRootNode As SyntaxNode)
        Try
            Console.WriteLine($"OnEditorDocumentParsed: Document parsed, root node = {If(vRootNode IsNot Nothing, vRootNode.Name, "Nothing")}")
            
            ' Update Object Explorer (existing functionality)
            UpdateObjectExplorerForActiveTab()
            UpdateNavigationDropdowns
            ' Update navigation dropdowns for current tab (new functionality)
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing Then
                Console.WriteLine("OnEditorDocumentParsed: Updating navigation dropdowns for current tab")
                UpdateNavigationDropdowns()
                
                ' Also update the position to reflect current cursor location
                If lCurrentTab.NavigationDropdowns IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                    Dim lLine As Integer = lCurrentTab.Editor.CurrentLine
                    Console.WriteLine($"OnEditorDocumentParsed: Updating position to line {lLine}")
                    lCurrentTab.NavigationDropdowns.UpdatePosition(lLine)
                End If
            End If
            
            ' Note: ProjectManager already has the parse info through SourceFileInfo
            ' No need to call UpdateFileParseInfo as it doesn't exist
            ' The parsing is already handled by ProjectManager.ParseFile
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorDocumentParsed error: {ex.Message}")
        End Try
    End Sub

    Private Sub OnGetThemeManager(vGTMEA As CustomDrawObjectExplorer.GetThemeManagerEventArgs)
        vGTMEA.ThemeManager = pThemeManager
    End Sub
    
    ''' <summary>
    ''' Update Object Explorer for the currently active tab
    ''' </summary>
    Private Sub UpdateObjectExplorerForActiveTab()
        Try
            If pObjectExplorer Is Nothing Then Return
            
            ' Get current tab
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab Is Nothing OrElse lCurrentTab.Editor Is Nothing Then Return
            
            ' Set the current editor
            pObjectExplorer.SetCurrentEditor(lCurrentTab.Editor)
            
            ' FIXED: If a project is open, always show the full project structure
            If pProjectManager IsNot Nothing AndAlso pProjectManager.IsProjectOpen Then
                ' Get and display the full project structure
                Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
                
                If lProjectTree IsNot Nothing Then
                    pObjectExplorer.UpdateStructure(lProjectTree)
                    Console.WriteLine("Object Explorer updated with project structure for active tab")
                Else
                    ' Fall back to file structure if project tree not available
                    Dim lStructure As SyntaxNode = lCurrentTab.Editor.GetDocumentStructure()
                    If lStructure IsNot Nothing Then
                        pObjectExplorer.UpdateStructure(lStructure)
                        Console.WriteLine("Object Explorer updated with file structure (project tree unavailable)")
                    End If
                End If
            Else
                ' No project open, show just the current file's structure
                Dim lStructure As SyntaxNode = lCurrentTab.Editor.GetDocumentStructure()
                If lStructure IsNot Nothing Then
                    pObjectExplorer.UpdateStructure(lStructure)
                    Console.WriteLine("Object Explorer updated with file structure (no project)")
                End If
            End If
            
            ' Update toolbar state
            UpdateObjectExplorerToolbarState()
            
        Catch ex As Exception
            Console.WriteLine($"UpdateObjectExplorerForActiveTab error: {ex.Message}")
        End Try
    End Sub
'     
'     ''' <summary>
'     ''' Set up editor for Object Explorer integration without redundant parsing
'     ''' </summary>
'     Private Sub SetupEditorForObjectExplorer(vEditor As IEditor)
'         Try
'             If vEditor Is Nothing Then Return
'             
'             ' Set up Object Explorer integration
'             SetupObjectExplorerForEditor(vEditor)
'             
' '             ' FIXED: Only trigger parsing if we don't already have structure
' '             Dim lCustomEditor As CustomDrawingEditor = TryCast(vEditor, CustomDrawingEditor)
' '             If lCustomEditor IsNot Nothing Then
' '                 ' Check if the editor already has parsed structure
' '                 Dim lStructure As SyntaxNode = lCustomEditor.GetDocumentStructure()
' '                 
' '                 If lStructure Is Nothing Then
' '                     ' No existing structure, request initial parse
' '                     Console.WriteLine("No existing structure, requesting parse")
' '                     lCustomEditor.RefreshSyntaxHighlighting()
' '                 Else
' '                     ' Already has structure, just update Object Explorer
' '                     Console.WriteLine("Editor already has parsed structure, updating Object Explorer")
' '                     If pObjectExplorer IsNot Nothing Then
' '                         ' If project is open, show full project structure
' '                         If pProjectManager IsNot Nothing AndAlso pProjectManager.IsProjectOpen Then
' '                             Dim lProjectTree As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
' '                             If lProjectTree IsNot Nothing Then
' '                                 pObjectExplorer.UpdateStructure(lProjectTree)
' '                             End If
' '                         Else
' '                             ' No project, show file structure
' '                             pObjectExplorer.UpdateStructure(lStructure)
' '                         End If
' '                     End If
' '                 End If
' '             End If
'             
'         Catch ex As Exception
'             Console.WriteLine($"SetupEditorForObjectExplorer error: {ex.Message}")
'         End Try
'     End Sub
    
    ''' <summary>
    ''' Object Explorer toolbar integration
    ''' </summary>
    Private Sub UpdateObjectExplorerToolbarState()
        Try
            If pObjectExplorer Is Nothing Then Return
            
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            Dim lHasActiveEditor As Boolean = (lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing)
            
            ' Enable/disable Object Explorer refresh button based on active editor
            pObjectExplorer.SetRefreshEnabled(lHasActiveEditor)
            
        Catch ex As Exception
            Console.WriteLine($"UpdateObjectExplorerToolbarState error: {ex.Message}")
        End Try
    End Sub

    ' Replace: SimpleIDE.MainWindow.OnObjectExplorerNavigateToFile
    Private Sub OnObjectExplorerNavigateToFile(vFilePath As String, vPosition As EditorPosition)
        Try
            Console.WriteLine($"NavigateToFile: {vFilePath} at line {vPosition.Line + 1}")
            
            ' Check if we need to open a different file
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            Dim lNeedToOpenFile As Boolean = True
            
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                If Not String.IsNullOrEmpty(lCurrentTab.FilePath) Then
                    If vFilePath.Equals(lCurrentTab.FilePath, StringComparison.OrdinalIgnoreCase) Then
                        lNeedToOpenFile = False
                    End If
                End If
            End If
            
            Dim lFileName As String = System.IO.Path.GetFileName(vFilePath)
            
            ' Show loading status ONLY if we need to open the file
            If lNeedToOpenFile Then
                UpdateStatusBar($"Loading {lFileName}...")
                
                ' Force immediate update and add tiny delay to ensure visibility
                While Gtk.Application.EventsPending()
                    Gtk.Application.RunIteration()
                End While
                System.Threading.Thread.Sleep(50) ' 50ms delay to ensure status is visible
            End If
            
            ' Open the file if needed
            If lNeedToOpenFile Then
                OpenFileWithProjectIntegration(vFilePath)
                lCurrentTab = GetCurrentTabInfo()
            End If
            
            ' Navigate to the position
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                ' vPosition is already 0-based from EditorPosition
                lCurrentTab.Editor.NavigateToLineNumberForPresentment(vPosition.Line)
                lCurrentTab.Editor.SelectLine(vPosition.Line + 1)  ' SelectLine expects 1-based
                lCurrentTab.Editor.Widget.GrabFocus()
                
                UpdateStatusBar($"Ready - {lFileName} at line {vPosition.Line + 1}")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnObjectExplorerNavigateToFile error: {ex.Message}")
            UpdateStatusBar("Ready")
        End Try
    End Sub

    
    
    ''' <summary>
    ''' Handle file opened from Project Explorer
    ''' </summary>
    Private Sub OnFileOpenedFromProjectExplorer(vEditor As IEditor)
        Try
            If pObjectExplorer Is Nothing OrElse vEditor Is Nothing Then Return
            
            Console.WriteLine("File opened from Project Explorer")
            
            ' Set the current editor
            pObjectExplorer.SetCurrentEditor(vEditor)
            
        Catch ex As Exception
            Console.WriteLine($"OnFileOpenedFromProjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Parse all project files and build complete project structure
    ''' </summary>
    Public Sub ParseProjectStructure(vProjectPath As String)
        Try
            Console.WriteLine($"ParseProjectStructure: {vProjectPath}")

            pProjectManager.LoadProjectWithParsing(vProjectPath)
            
            ' Get the combined project structure
            Dim lProjectStructure As SyntaxNode = pProjectManager.GetProjectSyntaxTree()
            
            If lProjectStructure IsNot Nothing Then
                Console.WriteLine($"  Project has {lProjectStructure.Children.Count} top-level nodes")
                
                ' Update Object Explorer with complete project structure
                If pObjectExplorer IsNot Nothing Then
                    pObjectExplorer.UpdateStructure(lProjectStructure)
                End If
            Else
                Console.WriteLine("  No project structure available")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"ParseProjectStructure error: {ex.Message}")
        End Try
    End Sub
    
    ' Replace: SimpleIDE.MainWindow.OnObjectExplorerNodeActivated


    ''' <summary>
    ''' Handle node selection in Object Explorer (single-click)
    ''' </summary>
    Private Sub OnObjectExplorerNodeSelected(vNode As SyntaxNode)
        Try
            If vNode Is Nothing Then Return
            
            ' Could highlight the node in editor or show info in status bar
            Console.WriteLine($"Object Explorer node selected: {vNode.Name}")
            
        Catch ex As Exception
            Console.WriteLine($"OnObjectExplorerNodeSelected error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Updated method to switch to the Object Explorer tab with proper activation
    ''' </summary>
    Private Sub SwitchToObjectExplorerTab()
        Try
            If pLeftNotebook IsNot Nothing AndAlso pObjectExplorer IsNot Nothing Then
                ' Find the Object Explorer page index
                for i As Integer = 0 To pLeftNotebook.NPages - 1
                    Dim lPage As Widget = pLeftNotebook.GetNthPage(i)
                    If lPage Is pObjectExplorer Then
                        pLeftNotebook.CurrentPage = i
                        Console.WriteLine($"Switched To Object Explorer tab (page {i})")
                        
                        ' Ensure it's visible and activated
                        lPage.ShowAll()
                        
                        ' Call OnPageActivated to ensure proper initialization
                        pObjectExplorer.OnPageActivated()
                        
                        Exit for
                    End If
                Next
            End If
        Catch ex As Exception
            Console.WriteLine($"SwitchToObjectExplorerTab error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Updated handler for left notebook page changes
    ''' </summary>
    Private Sub OnLeftNotebookPageChanged(vSender As Object, vArgs As SwitchPageArgs)
        Try
            Dim lNotebook As Notebook = TryCast(vSender, Notebook)
            If lNotebook Is Nothing Then Return
            
            Dim lNewPage As Widget = lNotebook.GetNthPage(CInt(vArgs.PageNum))
            
            ' If switching to Object Explorer, ensure it's properly activated
            If lNewPage Is pObjectExplorer Then
                Console.WriteLine("Switched To Object Explorer tab")
                
                ' Let GTK process the page switch first
                Application.Invoke(Sub()
                    ' Ensure the page is shown
                    lNewPage.ShowAll()
                    
                    ' Call OnPageActivated to ensure proper initialization
Console.WriteLine($"OnPageActivated called from MainWindow.OnLeftNotebookPageChanged")
                    pObjectExplorer.OnPageActivated()
                End Sub)
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnLeftNotebookPageChanged error: {ex.Message}")
        End Try
    End Sub

End Class
