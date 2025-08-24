' MainWindow.Keyboard.vb - Direct keyboard handling without accelerators
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models

Partial Public Class MainWindow
    
    ''' <summary>
    ''' Setup keyboard handling - simplified without accelerators
    ''' </summary>
    Private Sub SetupKeyboardShortcuts()
        Try
            Console.WriteLine("Setting up direct keyboard handling...")
            
            ' Connect the main window keyboard handler
            AddHandler Me.KeyPressEvent, AddressOf OnWindowKeyPress
            
            Console.WriteLine("Keyboard handling setup complete")
            
        Catch ex As Exception
            Console.WriteLine($"SetupKeyboardShortcuts error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Main keyboard handler for all window-level shortcuts
    ''' </summary>
    Private Sub OnWindowKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
        Try
            ' Get key info
            Dim lKeyString As String = KeyboardHelper.GetKeyString(vArgs.Event.KeyValue)
            Dim lModifiers As ModifierType = vArgs.Event.State
            
            ' Debug output for testing
            Console.WriteLine($"MainWindow Key: {lKeyString}, Modifiers: {lModifiers}")
            
            ' Don't process if a modal dialog is open
            ' CRITICAL FIX: Check if any dialogs are open as modal
            ' For now, just continue processing since we don't track modal dialogs
            ' If HasModalDialog() Then
            '     vArgs.RetVal = False
            '     Return
            ' End If
            
            ' ===== Handle Ctrl key combinations =====
            If (lModifiers And ModifierType.ControlMask) = ModifierType.ControlMask Then
                ' Handle Ctrl+Shift combinations first
                If (lModifiers And ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                    Select Case lKeyString.ToLower()
                        ' Ctrl+Shift+Z - Redo (standard alternative)
                        Case "z"
                            OnRedo(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        ' Other Ctrl+Shift combinations...
                        Case "s"
                            ' Ctrl+Shift+S - Save All
                            OnSaveAll(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "f"
                            ' Ctrl+Shift+F - Find in Files
                            ShowFindPanel()  ' CRITICAL FIX: Use existing ShowFindPanel method
                            vArgs.RetVal = True
                            Return
                            
                        Case "b"
                            ' Ctrl+Shift+B - Build Solution
                            OnBuildProject(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "p"
                            ' Ctrl+Shift+P - Command Palette (future feature)
                            ' ShowCommandPalette()
                            vArgs.RetVal = True
                            Return
                    End Select
                Else
                    ' Handle plain Ctrl combinations
                    Select Case lKeyString.ToLower()
                        ' File operations
                        Case "n"
                            ' Ctrl+N - New
                            OnNewFile(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "o"
                            ' Ctrl+O - Open
                            OnOpenFile(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "s"
                            ' Ctrl+S - Save
                            OnSaveFile(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "w"
                            ' Ctrl+W - Close Tab
                            OnCloseFile(Nothing, Nothing)  ' CRITICAL FIX: Use existing OnCloseFile method
                            vArgs.RetVal = True
                            Return
                            
                        Case "q"
                            ' Ctrl+Q - Quit
                            OnQuit(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        ' Edit operations
                        Case "z"
                            ' Ctrl+Z - Undo
                            OnUndo(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "r"
                            ' Ctrl+R - Redo (VB.NET standard alternative to Ctrl+Y)
                            OnRedo(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        ' NOTE: Ctrl+Y is NOT handled here - it's reserved for Cut Line in the editor
                            
                        Case "x"
                            ' Ctrl+X - Cut
                            OnCut(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "c"
                            ' Ctrl+C - Copy
                            OnCopy(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "v"
                            ' Ctrl+V - Paste
                            OnPaste(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "a"
                            ' Ctrl+A - Select All
                            OnSelectAll(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        ' Find/Replace
                        Case "f"
                            ' Ctrl+F - Find
                            OnFind(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "h"
                            ' Ctrl+H - Replace
                            OnReplace(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        Case "g"
                            ' Ctrl+G - Go to Line
                            OnGoToLine(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        ' Build operations
                        Case "b"
                            ' Ctrl+B - Build
                            OnBuildProject(Nothing, Nothing)
                            vArgs.RetVal = True
                            Return
                            
                        ' View operations
                        Case "e"
                            ' Ctrl+E - Toggle Project Explorer
                            ToggleProjectExplorer()
                            vArgs.RetVal = True
                            Return
                    End Select
                End If
            End If
            
            ' Let unhandled keys pass through to focused widget
            vArgs.RetVal = False
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowKeyPress error: {ex.Message}")
            vArgs.RetVal = False
        End Try
    End Sub
    
    
    ' ===== Helper Methods =====

    ''' <summary>
    ''' Closes the current tab with confirmation if modified
    ''' </summary>
    Private Sub CloseCurrentTab()
        Try
            Dim lCurrentTab As TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing Then
                CloseTab(lCurrentTab)
            End If
        Catch ex As Exception
            Console.WriteLine($"CloseCurrentTab error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Checks if a key string represents a modifier key
    ''' </summary>
    Private Function IsModifierKey(vKeyString As String) As Boolean
        Return vKeyString.StartsWith("Shift") OrElse
               vKeyString.StartsWith("Ctrl") OrElse
               vKeyString.StartsWith("Alt") OrElse
               vKeyString.StartsWith("Super") OrElse
               vKeyString = "CapsLock" OrElse
               vKeyString = "NumLock" OrElse
               vKeyString = "ScrollLock"
    End Function
    
    ''' <summary>
    ''' Handles the Escape key based on current context
    ''' </summary>
    Private Sub HandleEscapeKey()
        Try
            ' First priority: Close CodeSense if open
            Dim lCurrentTab As Models.TabInfo = GetCurrentTabInfo()
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                Try
                    Dim lEditor As IEditor = lCurrentTab.Editor
                    If TypeOf lEditor Is CustomDrawingEditor Then
                        DirectCast(lEditor, CustomDrawingEditor).CancelCodeSense()
                    End If
                Catch ex As Exception
                    ' CodeSense cancellation failed, continue
                End Try
            End If
            
            ' Second priority: Close find/replace panel if visible
            If pFindPanel IsNot Nothing AndAlso pFindPanel.Visible Then
                HideFindPanel()
                Return
            End If
            
            ' Third priority: Close bottom panel if visible
            If pBottomPanelVisible Then
                HideBottomPanel()
                Return
            End If
            
            ' Fourth priority: Clear selection in editor
            If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                lCurrentTab.Editor.ClearSelection()
            End If
            
        Catch ex As Exception
            Console.WriteLine($"HandleEscapeKey error: {ex.Message}")
        End Try
    End Sub
    
    ' ===== Stub Methods for New Features =====
    ' These can be implemented as needed
    
    Private Sub ShowFindInFilesDialog()
        Console.WriteLine("Find in Files - Not yet implemented")
        ' TODO: Implement find in files dialog
    End Sub
    
    
    Private Sub ToggleComment()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing Then
                ' TODO: Implement toggle comment in editor
                Console.WriteLine("Toggle Comment - Not yet implemented")
            End If
        Catch ex As Exception
            Console.WriteLine($"ToggleComment error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub DuplicateLine()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing Then
                ' TODO: Implement duplicate line in editor
                Console.WriteLine("Duplicate Line - Not yet implemented")
            End If
        Catch ex As Exception
            Console.WriteLine($"DuplicateLine error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub DeleteLine()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing Then
                ' TODO: Implement delete line in editor
                Console.WriteLine("Delete Line - Not yet implemented")
            End If
        Catch ex As Exception
            Console.WriteLine($"DeleteLine error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub ShowQuickOpen()
        Console.WriteLine("Quick Open - Not yet implemented")
        ' TODO: Implement quick open/command palette
    End Sub
    
    Private Sub ToggleProjectExplorer()
        Try
            ' Toggle left panel visibility
            If pMainHPaned.Position > 0 Then
                ' Save current position and hide
                pLastLeftPanelWidth = pMainHPaned.Position
                pMainHPaned.Position = 0
            Else
                ' Restore previous position
                pMainHPaned.Position = If(pLastLeftPanelWidth > 0, pLastLeftPanelWidth, 250)
            End If
        Catch ex As Exception
            Console.WriteLine($"ToggleProjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    Private pLastLeftPanelWidth As Integer = 250
    
    Private Sub ShowQuickFix()
        Console.WriteLine("Quick Fix - Not yet implemented")
        ' TODO: Implement quick fix/show properties
    End Sub
    
    Private Sub NavigateToPreviousHighlight()
        Console.WriteLine("Previous Highlight - Not yet implemented")
        ' TODO: Implement navigate to previous highlight
    End Sub
    
    Private Sub NavigateToNextHighlight()
        Console.WriteLine("Next Highlight - Not yet implemented")
        ' TODO: Implement navigate to next highlight
    End Sub
    
    Private Sub FindAllReferences()
        Console.WriteLine("Find All References - Not yet implemented")
        ' TODO: Implement find all references
    End Sub
    
    Private Sub RenameSymbol()
        Console.WriteLine("Rename Symbol - Not yet implemented")
        ' TODO: Implement rename symbol
    End Sub
    
    Private Sub NavigateToNextCompilationError()
        
Console.WriteLine("Next Compilation Error - Not yet implemented")
        ' TODO: Navigate to next compilation error
    End Sub
    
    Private Sub ToggleBreakpoint()
        Console.WriteLine("Toggle Breakpoint - Not yet implemented")
        ' TODO: Implement toggle breakpoint
    End Sub
    
    Private Sub StepOver()
        Console.WriteLine("Step Over - Not yet implemented")
        ' TODO: Implement step over debugging
    End Sub
    
    Private Sub StepOut()
        Console.WriteLine("Step Out - Not yet implemented")
        ' TODO: Implement step out debugging
    End Sub
    
    Private Sub GoToDefinition()
        Console.WriteLine("Go to Definition - Not yet implemented")
        ' TODO: Implement go to definition
    End Sub
    
    Private Sub OnRebuildProject(vSender As Object, vArgs As EventArgs)
        Try
            RebuildProject()
        Catch ex As Exception
            Console.WriteLine($"OnRebuildProject error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub OnBuildProject(vSender As Object, vArgs As EventArgs)
        Try
            BuildProject()
        Catch ex As Exception
            Console.WriteLine($"OnBuildProject error: {ex.Message}")
        End Try
    End Sub
    
'    Private Sub OnNavigateToNextError(vSender As Object, vArgs As EventArgs)
'        Try
'            ' Navigate to next error in build output
'            If pBuildOutputPanel IsNot Nothing Then
'                pBuildOutputPanel.NavigateToNextError()
'            End If
'        Catch ex As Exception
'            Console.WriteLine($"OnNavigateToNextError error: {ex.Message}")
'        End Try
'    End Sub
    
'    Private Sub OnNavigateToPreviousError(vSender As Object, vArgs As EventArgs)
'        Try
'            ' Navigate to previous error in build output
'            If pBuildOutputPanel IsNot Nothing Then
'                pBuildOutputPanel.NavigateToPreviousError()
'            End If
'        Catch ex As Exception
'            Console.WriteLine($"OnNavigateToPreviousError error: {ex.Message}")
'        End Try
'    End Sub
    

    
    Private Sub ShowHelpPanel(vSender As Object, vArgs As EventArgs)
        Try
            ' Show help panel
            ShowContextHelp("")
        Catch ex As Exception
            Console.WriteLine($"ShowHelpPanel error: {ex.Message}")
        End Try
    End Sub
    
End Class
