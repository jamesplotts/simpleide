' MainWindow.Keyboard.vb - Direct keyboard handling without accelerators
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models

Partial Public Class MainWindow


    Private pLastKeyEventTime As DateTime = DateTime.MinValue
    Private pLastKeyEventKey As Gdk.Key = Gdk.Key.VoidSymbol
    
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
    
    ' Replace: SimpleIDE.MainWindow.OnWindowKeyPress
    ' Replace: SimpleIDE.MainWindow.OnWindowKeyPress
    ''' <summary>
    ''' Main keyboard handler for all window-level shortcuts with fixed duplicate prevention
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Key press event arguments</param>
    ''' <remarks>
    ''' Fixed to prevent duplicates BEFORE calling event handlers
    ''' </remarks>
    Private Sub OnWindowKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
        ' Static variables for duplicate prevention
        Static sLastKeyTime As DateTime = DateTime.MinValue
        Static sLastKey As UInteger = 0
        
        Try
            ' Get current key and time
            Dim lCurrentTime As DateTime = DateTime.Now
            Dim lCurrentKey As UInteger = vArgs.Event.KeyValue
            
            ' Check for duplicate event FIRST (before processing)
            If lCurrentKey = sLastKey Then
                Dim lTimeDiff As TimeSpan = lCurrentTime - sLastKeyTime
                If lTimeDiff.TotalMilliseconds < 50 Then
                    Console.WriteLine($"Duplicate key event prevented: KeyValue={lCurrentKey}, TimeDiff={lTimeDiff.TotalMilliseconds:F4}ms")
                    vArgs.RetVal = True
                    Return  ' Exit early to prevent duplicate processing
                End If
            End If
            
            ' Update tracking variables
            sLastKeyTime = lCurrentTime
            sLastKey = lCurrentKey
            
            ' Get key info
            Dim lKeyString As String = KeyboardHelper.GetKeyString(vArgs.Event.KeyValue)
            Dim lModifiers As ModifierType = vArgs.Event.State
            
            ' Debug output for testing
            Console.WriteLine($"MainWindow Key: {lKeyString}, Modifiers: {lModifiers}")
            
            ' Filter out lock key modifiers (NumLock, CapsLock, ScrollLock) and Release mask
            Dim lCleanModifiers As ModifierType = lModifiers and Not (ModifierType.LockMask Or 
                                                                      ModifierType.Mod2Mask Or 
                                                                      ModifierType.ReleaseMask)
    
            ' ===== Handle Function Keys (F1-F12) without modifiers first =====
            If lCleanModifiers = ModifierType.None Then
                Select Case lKeyString
                    Case "F5"
                        ' F5 - Build and Run
                        Console.WriteLine("F5 pressed - Build and Run")
                        OnBuildAndRun(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "F6"
                        ' F6 - Build Project
                        Console.WriteLine("F6 pressed - Build Project")
                        OnBuildProject(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "F1"
                        ' F1 - Show Help
                        ShowContextHelp("")
                        vArgs.RetVal = True
                        Return
                        
                    Case "F11"
                        ' F11 - Toggle Full Screen
                        OnToggleFullScreen(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "F12"
                        ' F12 - Go to Definition
                        GoToDefinition()
                        vArgs.RetVal = True
                        Return
                End Select
            End If
            
            ' ===== Handle Shift + Function Key combinations =====
            If (lCleanModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask AndAlso
               (lCleanModifiers and ModifierType.ControlMask) <> ModifierType.ControlMask Then
                Select Case lKeyString
                    Case "F5"
                        ' Shift+F5 - Stop Debugging
                        OnStopDebugging(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                End Select
            End If
            
            ' ===== Handle Ctrl + Function Key combinations =====
            If (lCleanModifiers and ModifierType.ControlMask) = ModifierType.ControlMask AndAlso
               (lCleanModifiers and ModifierType.ShiftMask) <> ModifierType.ShiftMask Then
                Select Case lKeyString
                    Case "F5"
                        ' Ctrl+F5 - Run without debugging
                        ' TODO: OnRunWithoutDebugger(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "s"
                        ' Ctrl+S - Save
                        OnSaveFile(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "a"
                        ' Ctrl+A - Select All
                        OnSelectAll(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
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
                        
                    Case "b"
                        ' Ctrl+B - Build
                        OnBuildProject(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "e"
                        ' Ctrl+E - Toggle Project Explorer
                        ToggleProjectExplorer()
                        vArgs.RetVal = True
                        Return
                        
                    Case "tab"
                        ' Ctrl+Tab - Next Tab
                        SwitchToNextTab()
                        vArgs.RetVal = True
                        Return
                End Select
            End If
            
            ' ===== Handle Ctrl+Shift combinations =====
            If (lCleanModifiers and ModifierType.ControlMask) = ModifierType.ControlMask AndAlso
               (lCleanModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask Then
                
                Select Case lKeyString
                    Case "s"
                        ' Ctrl+Shift+S - Save All
                        OnSaveAll(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "f"
                        ' Ctrl+Shift+F - Find in Files
                        ' TODO: OnFindInFiles(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "b"
                        ' Ctrl+Shift+B - Build Solution
                         ' TODO: OnBuildSolution(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                End Select
            End If
            
            ' Let unhandled keys pass through to focused widget
            vArgs.RetVal = False
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowKeyPress error: {ex.Message}")
            vArgs.RetVal = False
        End Try
    End Sub

'     Private Sub OnWindowKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
'         Try
'             ' Get key info
'             Dim lKeyString As String = KeyboardHelper.GetKeyString(vArgs.Event.KeyValue)
'             Dim lModifiers As ModifierType = vArgs.Event.State
'             
'             ' Debug output for testing
'             Console.WriteLine($"MainWindow Key: {lKeyString}, Modifiers: {lModifiers}")
'             
'             ' Filter out lock key modifiers (NumLock, CapsLock, ScrollLock) and Release mask
'             ' These shouldn't affect function key detection
'             Dim lCleanModifiers As ModifierType = lModifiers and Not (ModifierType.LockMask Or 
'                                                                       ModifierType.Mod2Mask Or 
'                                                                       ModifierType.ReleaseMask)
' 
'             
'             ' ===== Handle Function Keys (F1-F12) without modifiers first =====
'             ' Check if no meaningful modifiers are pressed (ignoring NumLock, etc.)
'             If lCleanModifiers = ModifierType.None Then
'                 Select Case lKeyString
'                     Case "F5"
'                         ' F5 - Build and Run
'                         Console.WriteLine("F5 pressed - Build and Run")
'                         OnBuildAndRun(Nothing, Nothing)
'                         vArgs.RetVal = True
'                         Return
'                         
'                     Case "F6"
'                         ' F6 - Build Project
'                         Console.WriteLine("F6 pressed - Build Project")
'                         OnBuildProject(Nothing, Nothing)
'                         vArgs.RetVal = True
'                         Return
'                         
'                     Case "F1"
'                         ' F1 - Show Help
'                         ShowContextHelp("")
'                         vArgs.RetVal = True
'                         Return
'                         
'                     Case "F11"
'                         ' F11 - Toggle Full Screen
'                         OnToggleFullScreen(Nothing, Nothing)
'                         vArgs.RetVal = True
'                         Return
'                         
'                     Case "F12"
'                         ' F12 - Go to Definition
'                         GoToDefinition()
'                         vArgs.RetVal = True
'                         Return
'                 End Select
'             End If
'             
'             ' ===== Handle Shift + Function Key combinations =====
'             If (lCleanModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask AndAlso
'                (lCleanModifiers and ModifierType.ControlMask) <> ModifierType.ControlMask Then
'                 Select Case lKeyString
'                     Case "F5"
'                         ' Shift+F5 - Stop Debugging
'                         OnStopDebugging(Nothing, Nothing)
'                         vArgs.RetVal = True
'                         Return
'                 End Select
'             End If
'             
'             ' ===== Handle Ctrl + Function Key combinations =====
'             If (lCleanModifiers and ModifierType.ControlMask) = ModifierType.ControlMask AndAlso
'                (lCleanModifiers and ModifierType.ShiftMask) <> ModifierType.ShiftMask Then
'                 Select Case lKeyString
'                     Case "F5"
'                         ' Ctrl+F5 - Run without debugging (just run, don't build)
'                         Task.Run(Async Function()
'                             Await RunProject()
'                             Return Nothing
'                         End Function)
'                         vArgs.RetVal = True
'                         Return
'                 End Select
'             End If
'             
'             ' ===== Handle Ctrl key combinations =====
'             If (lCleanModifiers and ModifierType.ControlMask) = ModifierType.ControlMask Then
'                 ' Handle Ctrl+Shift combinations first
'                 If (lCleanModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask Then
'                     Select Case lKeyString.ToLower()
'                         ' Ctrl+Shift+Z - Redo (standard alternative)
'                         Case "z"
'                             OnRedo(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         ' Ctrl+Shift+B - Build Solution
'                         Case "b"
'                             RebuildProject
'                             vArgs.RetVal = True
'                             Return
'                             
'                         ' Ctrl+Shift+F - Find in Files
'                         Case "f"
'                             OnFind(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         ' Ctrl+Shift+Tab - Previous Tab
'                         Case "tab", "iso_left_tab"
'                             SwitchToPreviousTab()
'                             vArgs.RetVal = True
'                             Return
'                     End Select
'                 End If
'                 
'                 ' Handle regular Ctrl combinations (no Shift)
'                 If (lCleanModifiers and ModifierType.ShiftMask) <> ModifierType.ShiftMask Then
'                     Select Case lKeyString.ToLower()
'                         ' File operations
'                         Case "n"
'                             ' Ctrl+N - New File
'                             OnNewFile(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "o"
'                             ' Ctrl+O - Open File
'                             OnOpenFile(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "s"
'                             ' Ctrl+S - Save File
'                             OnSaveFile(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "w"
'                             ' Ctrl+W - Close Tab
'                             CloseCurrentTab()
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "q"
'                             ' Ctrl+Q - Quit
'                             OnWindowDelete(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         ' Edit operations
'                         Case "z"
'                             ' Ctrl+Z - Undo
'                             OnUndo(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "r"
'                             ' Ctrl+R - Redo
'                             OnRedo(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "y"
'                             ' Ctrl+Y - Cut Line (VB.NET tradition)
'                             OnCutLine(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "x"
'                             ' Ctrl+X - Cut
'                             OnCut(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "c"
'                             ' Ctrl+C - Copy
'                             OnCopy(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "v"
'                             ' Ctrl+V - Paste
'                             OnPaste(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "a"
'                             ' Ctrl+A - Select All
'                             OnSelectAll(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         ' Search operations
'                         Case "f"
'                             ' Ctrl+F - Find
'                             OnFind(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "h"
'                             ' Ctrl+H - Replace
'                             OnReplace(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         Case "g"
'                             ' Ctrl+G - Go to Line
'                             OnGoToLine(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         ' Build operations
'                         Case "b"
'                             ' Ctrl+B - Build
'                             OnBuildProject(Nothing, Nothing)
'                             vArgs.RetVal = True
'                             Return
'                             
'                         ' View operations
'                         Case "e"
'                             ' Ctrl+E - Toggle Project Explorer
'                             ToggleProjectExplorer()
'                             vArgs.RetVal = True
'                             Return
'                             
'                         ' Tab navigation
'                         Case "tab"
'                             ' Ctrl+Tab - Next Tab
'                             SwitchToNextTab()
'                             vArgs.RetVal = True
'                             Return
'                     End Select
'                 End If
'             End If
'             
'             ' Let unhandled keys pass through to focused widget
'             vArgs.RetVal = False
'             
'         Catch ex As Exception
'             Console.WriteLine($"OnWindowKeyPress error: {ex.Message}")
'             vArgs.RetVal = False
'         End Try
'     End Sub
    
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
    
    ''' <summary>
    ''' Handles Build Project command (F6)
    ''' </summary>
    Public Sub OnBuildProject(vSender As Object, vArgs As EventArgs)
        Try
            Console.WriteLine("OnBuildProject called")
            
            ' Check if already building using BuildManager
            If pBuildManager IsNot Nothing AndAlso pBuildManager.IsBuilding Then
                Console.WriteLine("OnBuildProject: Build already in progress")
                ShowInfo("Build in Progress", "A build is already in progress.")
                Return
            End If
            
            ' Call the BuildProject method
            BuildProject()
            
        Catch ex As Exception
            Console.WriteLine($"OnBuildProject error: {ex.Message}")
            ShowError("Build Error", ex.Message)
        End Try
    End Sub
    
    Private Sub ShowHelpPanel(vSender As Object, vArgs As EventArgs)
        Try
            ' Show help panel
            ShowContextHelp("")
        Catch ex As Exception
            Console.WriteLine($"ShowHelpPanel error: {ex.Message}")
        End Try
    End Sub

End Class