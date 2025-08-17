' MainWindow.Editor.Enhanced.vb - Enhanced editor event handling with Object Explorer integration

Imports Gtk
Imports System
Imports SimpleIDE.Interfaces

Partial Public Class MainWindow
    Inherits Window
    
' MainWindow.Editor.Enhanced.vb
' Created: 2025-08-06 15:13:33

    ' ===== Enhanced Editor Event Handlers =====
    
    ' Enhanced notebook switch page handler with Object Explorer integration
    Private Sub OnNotebookSwitchPageEnhanced(vSender As Object, vArgs As SwitchPageArgs)
        Try
            ' Call existing switch page logic
            OnNotebookSwitchPage(vSender, vArgs)
            
            ' Update Object Explorer for new active editor
            UpdateObjectExplorerForActiveTab()
            
            ' Update Object Explorer toolbar state
            UpdateObjectExplorerToolbarState()
            
        Catch ex As Exception
            Console.WriteLine($"OnNotebookSwitchPageEnhanced error: {ex.Message}")
        End Try
    End Sub
    
    ' Enhanced editor creation with Object Explorer setup
    Private Sub OnEditorCreatedEnhanced(vEditor As IEditor)
        Try
            ' Call existing creation logic if it exists
            ' OnEditorCreated(vEditor)
            
            ' Set up Object Explorer integration for new editor
            SetupEditorForObjectExplorer(vEditor)
            
            ' Hook up focus events for Object Explorer updates
            Dim lWidget As Widget = vEditor.Widget
            If lWidget IsNot Nothing Then
                AddHandler lWidget.FocusInEvent, Sub(o, e)
                    If pObjectExplorer IsNot Nothing Then
                        ' Just set the current editor directly in Object Explorer
                        If pObjectExplorer IsNot Nothing Then
                            pObjectExplorer.SetCurrentEditor(lCurrentTab.Editor)
                            
                            ' Hook up the DocumentParsed event if not already connected
                            RemoveHandler lCurrentTab.Editor.DocumentParsed, AddressOf OnEditorDocumentParsed
                            AddHandler lCurrentTab.Editor.DocumentParsed, AddressOf OnEditorDocumentParsed
                            
                            ' Get initial document structure if available
                            Dim lStructure As SyntaxNode = lCurrentTab.Editor.GetDocumentStructure()
                            If lStructure IsNot Nothing Then
                                pObjectExplorer.UpdateStructure(lStructure)
                            End If
                        End If
                    End If
                End Sub
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorCreatedEnhanced error: {ex.Message}")
        End Try
    End Sub
    
    ' Enhanced text changed handler with Object Explorer considerations
    Public Sub OnEditorTextChangedEnhanced(vSender As Object, vArgs As EventArgs)
        Try
            ' Get the editor that changed
            Dim lEditor As IEditor = TryCast(vSender, IEditor)
            If lEditor Is Nothing Then Return
            
            ' Update modified state in UI
            UpdateTabModifiedState(lEditor)
            
            ' Update status bar if this is the current editor
            If lEditor Is GetCurrentEditor() Then
                UpdateStatusBar()
            End If
            
            ' Mark project as dirty if needed
            If pProjectManager IsNot Nothing Then
                pProjectManager.MarkDirty()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorTextChanged error: {ex.Message}")
        End Try
    End Sub
    
    ' Method to replace existing event handler hookups
    Private Sub HookupEditorEventsForObjectExplorer(vEditor As IEditor)
        Try
            If vEditor Is Nothing Then Return
            
            ' Hook up standard editor events
            AddHandler vEditor.Modified, AddressOf OnEditorModified
            AddHandler vEditor.CursorPositionChanged, AddressOf OnEditorCursorPositionChanged
            AddHandler vEditor.SelectionChanged, AddressOf OnEditorSelectionChanged
            AddHandler vEditor.TextChanged, AddressOf OnEditorTextChangedEnhanced
            AddHandler vEditor.UndoRedoStateChanged, AddressOf OnEditorUndoRedoStateChanged
            
            ' Hook up Object Explorer-specific events
            SetupEditorForObjectExplorer(vEditor)
            
        Catch ex As Exception
            Console.WriteLine($"HookupEditorEventsForObjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ' Method to unhook events when closing tabs
    Private Sub UnhookEditorEventsForObjectExplorer(vEditor As IEditor)
        Try
            If vEditor Is Nothing Then Return
            
            ' Unhook standard events
            RemoveHandler vEditor.Modified, AddressOf OnEditorModified
            RemoveHandler vEditor.CursorPositionChanged, AddressOf OnEditorCursorPositionChanged
            RemoveHandler vEditor.SelectionChanged, AddressOf OnEditorSelectionChanged
            RemoveHandler vEditor.TextChanged, AddressOf OnEditorTextChangedEnhanced
            RemoveHandler vEditor.UndoRedoStateChanged, AddressOf OnEditorUndoRedoStateChanged
            
            ' Unhook Object Explorer events
            RemoveHandler vEditor.DocumentParsed, AddressOf OnEditorDocumentParsed
            
        Catch ex As Exception
            Console.WriteLine($"UnhookEditorEventsForObjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    ' Missing event handlers that may be referenced
    Private Sub OnEditorModified(vIsModified As Boolean)
        Try
            ' Find the editor that sent this event and mark tab as modified
            UpdateStatusBar("")
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorModified error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnEditorUndoRedoStateChanged(vCanUndo As Boolean, vCanRedo As Boolean)
        Try
            ' Update toolbar buttons
            UpdateToolbarButtons()
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorUndoRedoStateChanged error: {ex.Message}")
        End Try
    End Sub
    
End Class
