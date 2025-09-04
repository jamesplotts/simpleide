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

                    Case "F2"
                        ' F2 - Quick Find from Clipboard
                        Console.WriteLine("F2 pressed - Quick Find from Clipboard")
                        OnQuickFindFromClipboard(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "F1"
                        ' F1 - Show Help in a tab
                        Console.WriteLine("F1 pressed - Show Help")
                        Dim lContext As String = GetCurrentHelpContext()
                        If Not String.IsNullOrEmpty(lContext) Then
                            ShowContextHelpInTab(lContext)
                        Else
                            OpenHelpTab()
                        End If
                        vArgs.RetVal = True
                        Return      
                                      
                    Case "F3"
                        ' F3 - Find Next
                        Console.WriteLine("F3 pressed - Find Next")
                        FindNextOccurrence()
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
                    Case "F3"
                        ' Shift+F3 - Find Previous
                        Console.WriteLine("Shift+F3 pressed - Find Previous")
                        FindPreviousOccurrence()
                        vArgs.RetVal = True
                        Return

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

            ' ===== Handle Alt Key combinations =====
            If (lCleanModifiers and ModifierType.Mod1Mask) = ModifierType.Mod1Mask AndAlso
               (lCleanModifiers and ModifierType.ControlMask) <> ModifierType.ControlMask Then
                Select Case lKeyString
                    Case "Left"
                        ' Alt+Left - Navigate back in help tab
                        If IsCurrentTabHelp() Then
                            NavigateHelpBack()
                            vArgs.RetVal = True
                            Return
                        End If
                        
                    Case "Right"
                        ' Alt+Right - Navigate forward in help tab
                        If IsCurrentTabHelp() Then
                            NavigateHelpForward()
                            vArgs.RetVal = True
                            Return
                        End If
                        
                    Case "Home"
                        ' Alt+Home - Navigate to help home
                        If IsCurrentTabHelp() Then
                            NavigateHelpHome()
                            vArgs.RetVal = True
                            Return
                        End If
                End Select
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
    ''' Helper method to close the current tab
    ''' </summary>
    Public Sub CloseCurrentTab()
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
    ''' Handles Build Project command (F6) - Build only, no run
    ''' </summary>
    ''' <param name="vSender">Event sender</param>
    ''' <param name="vArgs">Event arguments</param>
    Public Sub OnBuildProject(vSender As Object, vArgs As EventArgs)
        Try
            Console.WriteLine("OnBuildProject called (F6 - Build Only)")
            
            ' Check if already building
            If pIsBuildingNow Then
                Console.WriteLine("OnBuildProject: Already building (pIsBuildingNow check)")
                ShowInfo("Build in Progress", "A build is already in progress.")
                Return
            End If
            
            ' Check if already building using BuildManager
            If pBuildManager IsNot Nothing AndAlso pBuildManager.IsBuilding Then
                Console.WriteLine("OnBuildProject: Build already in progress (BuildManager check)")
                ShowInfo("Build in Progress", "A build is already in progress.")
                Return
            End If
            
            ' IMPORTANT: Make sure we DON'T set pRunAfterBuild flag
            ' This ensures it's build-only, not build-and-run
            pRunAfterBuild = False
            
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

    ''' <summary>
    ''' Handles F1 key press for context-sensitive help
    ''' </summary>
    ''' <param name="vKey">The key that was pressed</param>
    ''' <returns>True if handled, False otherwise</returns>
    Private Function HandleF1Help(vKey As Gdk.Key) As Boolean
        Try
            If vKey = Gdk.Key.F1 Then
                ' Get current context for help
                Dim lContext As String = GetCurrentHelpContext()
                
                If Not String.IsNullOrEmpty(lContext) Then
                    ' Show context-specific help
                    ShowContextHelpInTab(lContext)
                Else
                    ' Show general help
                    OpenHelpTab()
                End If
                
                Return True
            End If
            
            Return False
            
        Catch ex As Exception
            Console.WriteLine($"HandleF1Help error: {ex.Message}")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Gets keyboard shortcuts help text including F2 for Quick Find
    ''' </summary>
    ''' <returns>Formatted help text for keyboard shortcuts</returns>
    Private Function GetKeyboardShortcutsHelp() As String
        Dim lText As New System.Text.StringBuilder()
        
        lText.AppendLine("KEYBOARD SHORTCUTS")
        lText.AppendLine("==================")
        lText.AppendLine()
        
        lText.AppendLine("File Operations:")
        lText.AppendLine("  Ctrl+N          New File")
        lText.AppendLine("  Ctrl+O          Open File")
        lText.AppendLine("  Ctrl+S          Save File")
        lText.AppendLine("  Ctrl+Shift+S    Save All Files")
        lText.AppendLine("  Ctrl+W          Close Tab")
        lText.AppendLine()
        
        lText.AppendLine("Edit Operations:")
        lText.AppendLine("  Ctrl+Z          Undo")
        lText.AppendLine("  Ctrl+R          Redo")
        lText.AppendLine("  Ctrl+Shift+Z    Redo (alternate)")
        lText.AppendLine("  Ctrl+X          Cut")
        lText.AppendLine("  Ctrl+C          Copy")
        lText.AppendLine("  Ctrl+V          Paste")
        lText.AppendLine("  Ctrl+A          Select All")
        lText.AppendLine("  Ctrl+Y          Cut Line (VB style)")
        lText.AppendLine("  Ctrl+D          Duplicate Line")
        lText.AppendLine()
        
        lText.AppendLine("Search Operations:")
        lText.AppendLine("  Ctrl+F          Find")
        lText.AppendLine("  Ctrl+H          Replace")
        lText.AppendLine("  F2              Quick Find (clipboard text)")
        lText.AppendLine("  F3              Find Next")
        lText.AppendLine("  Shift+F3        Find Previous")
        lText.AppendLine("  Ctrl+G          Go to Line")
        lText.AppendLine()
        
        lText.AppendLine("Build/Debug Operations:")
        lText.AppendLine("  F5              Build and Run")
        lText.AppendLine("  F6              Build Only")
        lText.AppendLine("  Ctrl+F5         Run without debugging")
        lText.AppendLine("  Shift+F5        Stop debugging")
        lText.AppendLine("  Ctrl+Shift+B    Build Solution")
        lText.AppendLine("  F12             Go to Definition")
        lText.AppendLine()
        
        lText.AppendLine("View Operations:")
        lText.AppendLine("  Ctrl+E          Toggle Project Explorer")
        lText.AppendLine("  F11             Toggle Full Screen")
        lText.AppendLine("  Ctrl+Tab        Next Tab")
        lText.AppendLine("  Ctrl+Shift+Tab  Previous Tab")
        lText.AppendLine()
        
        lText.AppendLine("Text Navigation:")
        lText.AppendLine("  Ctrl+Home       Go to start of document")
        lText.AppendLine("  Ctrl+End        Go to end of document")
        lText.AppendLine("  Ctrl+Left       Previous word")
        lText.AppendLine("  Ctrl+Right      Next word")
        lText.AppendLine("  Home            Start of line")
        lText.AppendLine("  End             End of line")
        lText.AppendLine("  Page Up         Page up")
        lText.AppendLine("  Page Down       Page down")
        lText.AppendLine()
        
        lText.AppendLine("Special Keys:")
        lText.AppendLine("  Tab             Indent/Accept IntelliSense")
        lText.AppendLine("  Shift+Tab       Outdent")
        lText.AppendLine("  Escape          Cancel operation/Clear selection")
        lText.AppendLine("  Ctrl+Space      Trigger IntelliSense")
        lText.AppendLine("  Ctrl+Shift+Space  Parameter hints")
        lText.AppendLine()
        
        lText.AppendLine("Quick Find (F2) Tip:")
        lText.AppendLine("  Copy any text to clipboard, then press F2 to instantly")
        lText.AppendLine("  search for it across your entire project.")
        lText.AppendLine()
        
        lText.AppendLine("Note: Ctrl+Y is the traditional VB 'Cut Line' command,")
        lText.AppendLine("      not Redo. Use Ctrl+R or Ctrl+Shift+Z for Redo.")
        
        Return lText.ToString()
    End Function

End Class