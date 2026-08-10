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

    ' Held for the app's lifetime so the native GTK side's callback pointer never outlives
    ' its managed delegate (Gtk.Key.SnooperInstall keeps a reference internally too, but
    ' this matches the same defensive pattern already used for other long-lived native
    ' callback delegates in this codebase, e.g. CustomDrawingEditor's pKeyPressHandler)
    Private pTabCyclingSnooper As Gtk.KeySnoopFunc

    ' Replace: SimpleIDE.MainWindow.SetupKeyboardShortcuts
    ''' <summary>
    ''' Setup keyboard handling - simplified without accelerators
    ''' </summary>
    Private Sub SetupKeyboardShortcuts()
        Try
            #If DEBUG Then
            Console.WriteLine("Setting up direct keyboard handling...")
            #End If

            ' Connect the main window keyboard handler
            AddHandler Me.KeyPressEvent, AddressOf OnWindowKeyPress

#If DEBUG
            ' ADD: Connect the diagnostic keyboard handler
            AddHandler Me.KeyPressEvent, AddressOf OnKeyPressForDiagnostics
#End If

            ' Ctrl+Tab / Ctrl+Shift+Tab need a global key snooper, not a plain
            ' KeyPressEvent handler - see InstallTabCyclingSnooper for why
            InstallTabCyclingSnooper()

            #If DEBUG Then
            Console.WriteLine("Keyboard handling setup complete")
            #End If

        Catch ex As Exception
            Console.WriteLine($"SetupKeyboardShortcuts error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Installs a GTK key snooper to implement Ctrl+Tab/Ctrl+Shift+Tab cycling through the
    ''' main panel's tabs (editors, Help, Scratchpad, etc.), working no matter where
    ''' keyboard focus currently is in the IDE
    ''' </summary>
    ''' <remarks>
    ''' A plain KeyPressEvent handler on Me (the window) is not enough: GtkWidget has a
    ''' built-in default key binding on Tab for focus navigation (move-focus), and whichever
    ''' widget currently has keyboard focus gets first chance at the event and consumes it
    ''' via that binding before it would ever reach this window's own key-press-event
    ''' handler - this is why Ctrl+Tab previously did nothing while, say, the code editor,
    ''' the Help tab's URL bar, or a bottom panel text box had focus. A key snooper
    ''' (Gtk.Key.SnooperInstall) runs before any of that normal per-widget dispatch, for
    ''' every key event in the whole application, so it always gets first chance regardless
    ''' of focus. The action itself is scoped to pNotebook (the main panel) unconditionally,
    ''' via SwitchToNextTab/SwitchToPreviousTab - it never touches the bottom panel's own
    ''' tabs, no matter which widget had focus when the shortcut was pressed.
    ''' </remarks>
    Private Sub InstallTabCyclingSnooper()
        Try
            pTabCyclingSnooper = New Gtk.KeySnoopFunc(AddressOf OnTabCyclingKeySnoop)
            Gtk.Key.SnooperInstall(pTabCyclingSnooper)
        Catch ex As Exception
            Console.WriteLine($"InstallTabCyclingSnooper error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Global key snoop callback - handles Ctrl+Tab/Ctrl+Shift+Tab from anywhere in the
    ''' IDE by cycling the main panel's tabs, and otherwise leaves every other key event
    ''' alone
    ''' </summary>
    ''' <param name="vGrabWidget">The widget that would normally receive this event</param>
    ''' <param name="vEvent">The raw key event</param>
    ''' <returns>1 to consume the event (stop normal delivery), 0 to let it proceed as usual</returns>
    Private Function OnTabCyclingKeySnoop(vGrabWidget As Widget, vEvent As Gdk.EventKey) As Integer
        Const KEY_TAB As UInteger = 65289
        Const KEY_ISO_LEFT_TAB As UInteger = 65056
        Try
            If vEvent.Type <> Gdk.EventType.KeyPress Then Return 0

            Dim lModifiers As ModifierType = vEvent.State And Not (ModifierType.LockMask Or
                                                                     ModifierType.Mod2Mask Or
                                                                     ModifierType.ReleaseMask)
            If (lModifiers and ModifierType.ControlMask) <> ModifierType.ControlMask Then Return 0

            Dim lHasShift As Boolean = (lModifiers and ModifierType.ShiftMask) = ModifierType.ShiftMask
            Dim lIsPrevious As Boolean = vEvent.KeyValue = KEY_ISO_LEFT_TAB OrElse
                                          (vEvent.KeyValue = KEY_TAB AndAlso lHasShift)
            Dim lIsNext As Boolean = vEvent.KeyValue = KEY_TAB AndAlso Not lHasShift

            If Not lIsPrevious AndAlso Not lIsNext Then Return 0
            If pNotebook Is Nothing Then Return 0

            If lIsPrevious Then
                SwitchToPreviousTab()
            Else
                SwitchToNextTab()
            End If

            Return 1

        Catch ex As Exception
            Console.WriteLine($"OnTabCyclingKeySnoop error: {ex.Message}")
            Return 0
        End Try
    End Function

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
                    #If DEBUG Then
                    Console.WriteLine($"Duplicate key event prevented: KeyValue={lCurrentKey}, TimeDiff={lTimeDiff.TotalMilliseconds:F4}ms")
                    #End If
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
            #If DEBUG Then
            Console.WriteLine($"MainWindow Key: {lKeyString}, Modifiers: {lModifiers}")
            #End If
            
            ' Filter out lock key modifiers (NumLock, CapsLock, ScrollLock) and Release mask
            Dim lCleanModifiers As ModifierType = lModifiers and Not (ModifierType.LockMask Or 
                                                                      ModifierType.Mod2Mask Or 
                                                                      ModifierType.ReleaseMask)
    
            ' ===== Handle Function Keys (F1-F12) without modifiers first =====
            If lCleanModifiers = ModifierType.None Then
                Select Case lKeyString
                    Case "F5"
                        ' F5 - Build and Run
                        #If DEBUG Then
                        Console.WriteLine("F5 pressed - Build and Run")
                        #End If
                        OnBuildAndRun(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "F6"
                        ' F6 - Build Project
                        #If DEBUG Then
                        Console.WriteLine("F6 pressed - Build Project")
                        #End If
                        OnBuildProject(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return

                    Case "F2"
                        ' F2 - Quick Find from Clipboard
                        #If DEBUG Then
                        Console.WriteLine("F2 pressed - Quick Find from Clipboard")
                        #End If
                        OnQuickFindFromClipboard(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return
                        
                    Case "F1"
                        ' F1 - Show Help in a tab
                        #If DEBUG Then
                        Console.WriteLine("F1 pressed - Show Help")
                        #End If
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
                        #If DEBUG Then
                        Console.WriteLine("F3 pressed - Find Next")
                        #End If
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

                    Case "F8"
                        ' F8 - Next Build Error
                        NavigateToNextCompilationError()
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
                        #If DEBUG Then
                        Console.WriteLine("Shift+F3 pressed - Find Previous")
                        #End If
                        FindPreviousOccurrence()
                        vArgs.RetVal = True
                        Return

                    Case "F5"
                        ' Shift+F5 - Stop Debugging
                        OnStopDebugging(Nothing, Nothing)
                        vArgs.RetVal = True
                        Return

                    Case "F12"
                        ' Shift+F12 - Find All References
                        FindAllReferences()
                        vArgs.RetVal = True
                        Return

                    Case "F6"
                        ' Shift+F6 - Rename Symbol
                        RenameSymbol()
                        vArgs.RetVal = True
                        Return

                    Case "F8"
                        ' Shift+F8 - Previous Build Error
                        NavigateToPreviousError()
                        vArgs.RetVal = True
                        Return
                End Select
            End If

            ' ===== Handle Ctrl + Function Key combinations =====
            If (lCleanModifiers and ModifierType.ControlMask) = ModifierType.ControlMask AndAlso
               (lCleanModifiers and ModifierType.ShiftMask) <> ModifierType.ShiftMask Then
                Select Case lKeyString
                    Case "F5"
                        ' Ctrl+F5 - Run without building. Menu label advertises this shortcut
                        ' (MainWindow.Menu.vb's "Run Without Building" item) but this handler
                        ' was a bare TODO stub that just swallowed the key - the real method to
                        ' call already existed under a different name.
                        OnRunWithoutBuilding(Nothing, Nothing)
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

                    Case "p"
                        ' Ctrl+P - Quick Open (go to file)
                        ShowQuickOpen()
                        vArgs.RetVal = True
                        Return

                    Case "/"
                        ' Ctrl+/ - Toggle Comment. Calls the toolbar's own working
                        ' OnToggleComment (MainWindow.Comment.vb) - NOT the separate,
                        ' still-unimplemented ToggleComment() stub further down in this
                        ' file, which is unrelated dead code
                        OnToggleComment(Nothing, Nothing)
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
                        FindInFiles()
                        vArgs.RetVal = True
                        Return

                    Case "b"
                        ' Ctrl+Shift+B - Build Solution
                        OnBuildSolution(Nothing, Nothing)
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

            ' ===== Handle Escape =====
            ' HandleEscapeKey's own priority chain (close CodeSense, close Find/Replace
            ' panel, close bottom panel, clear selection) previously had no caller anywhere
            ' - CustomDrawingEditor's own Escape handling always consumed the key itself
            ' when the editor had focus (the common case), so this never ran. The editor
            ' now only consumes Escape when it actually cleared a selection/CodeSense popup,
            ' letting it bubble up here otherwise.
            If lKeyString = "Escape" AndAlso lCleanModifiers = ModifierType.None Then
                HandleEscapeKey()
                vArgs.RetVal = True
                Return
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
    
    Private Sub ToggleComment()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor IsNot Nothing Then
                lEditor.ToggleCommentBlock()
            End If
        Catch ex As Exception
            Console.WriteLine($"ToggleComment error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Inserts a copy of the current line directly below it, moving the cursor down to the
    ''' duplicate at the same column
    ''' </summary>
    Private Sub DuplicateLine()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor Is Nothing Then Return

            Dim lCursor As EditorPosition = lEditor.GetCursorPosition()
            Dim lLineText As String = lEditor.GetLineText(lCursor.Line)

            lEditor.InsertTextAtPosition(New EditorPosition(lCursor.Line, lLineText.Length), Environment.NewLine & lLineText)
            lEditor.SetCursorPosition(New EditorPosition(lCursor.Line + 1, lCursor.Column))

        Catch ex As Exception
            Console.WriteLine($"DuplicateLine error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Deletes the entire current line, including its line break, so the lines below shift up
    ''' </summary>
    Private Sub DeleteLine()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor Is Nothing Then Return

            Dim lCursor As EditorPosition = lEditor.GetCursorPosition()
            Dim lLine As Integer = lCursor.Line

            If lEditor.LineCount <= 1 Then
                ' Only line in the file - nothing to remove it "into", so just clear it
                Dim lLineText As String = lEditor.GetLineText(lLine)
                lEditor.DeleteRange(New EditorPosition(lLine, 0), New EditorPosition(lLine, lLineText.Length))
                lEditor.SetCursorPosition(New EditorPosition(lLine, 0))
                Return
            End If

            If lLine < lEditor.LineCount - 1 Then
                ' Delete the line plus its trailing line break - the next line slides up to
                ' become this line
                lEditor.DeleteRange(New EditorPosition(lLine, 0), New EditorPosition(lLine + 1, 0))
                lEditor.SetCursorPosition(New EditorPosition(lLine, 0))
            Else
                ' Last line has no trailing line break to eat - remove the preceding one instead
                ' so a line actually disappears, landing the cursor at the end of what's now the
                ' last line
                Dim lPrevLineLength As Integer = lEditor.GetLineText(lLine - 1).Length
                Dim lLineText As String = lEditor.GetLineText(lLine)
                lEditor.DeleteRange(New EditorPosition(lLine - 1, lPrevLineLength), New EditorPosition(lLine, lLineText.Length))
                lEditor.SetCursorPosition(New EditorPosition(lLine - 1, lPrevLineLength))
            End If

        Catch ex As Exception
            Console.WriteLine($"DeleteLine error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Ctrl+P - "quick open" file switcher: lists every compiled source file in the
    ''' current project (every project in a loaded solution, when one is loaded),
    ''' live-filtered by filename substring as the user types
    ''' </summary>
    Private Sub ShowQuickOpen()
        Try
            Dim lFileSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim lFiles As New List(Of String)

            If pSolutionManager IsNot Nothing AndAlso pSolutionManager.AllProjects.Count > 0 Then
                for each lProject in pSolutionManager.AllProjects
                    for each lFile in lProject.GetProjectSourceFiles()
                        If lFileSet.Add(lFile) Then lFiles.Add(lFile)
                    Next
                Next
            ElseIf pProjectManager IsNot Nothing Then
                lFiles.AddRange(pProjectManager.GetProjectSourceFiles())
            End If

            If lFiles.Count = 0 Then
                UpdateStatusBar("Quick Open: No project files to search")
                Return
            End If

            Using lDialog As New Dialogs.QuickOpenDialog(Me, lFiles, pThemeManager)
                If lDialog.Run() = CInt(ResponseType.Ok) AndAlso Not String.IsNullOrEmpty(lDialog.SelectedFile) Then
                    OpenFile(lDialog.SelectedFile)
                    SwitchToTab(lDialog.SelectedFile)
                End If
            End Using

        Catch ex As Exception
            Console.WriteLine($"ShowQuickOpen error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Toggles the left panel (Project/Object Explorer notebook) by collapsing/restoring
    ''' pMainHPaned's split position - the only mechanism that actually reclaims the panel's
    ''' width (unlike toggling pProjectExplorer.Visible alone, which only blanks the current
    ''' notebook page and leaves the panel's width and tab strip in place). This is now the
    ''' single implementation for both Ctrl+E and the View menu's "Project Explorer" item -
    ''' OnToggleProjectExplorer (MainWindow.Events.vb) delegates here instead of maintaining
    ''' its own pProjectExplorer.Visible-based toggle, which had fallen out of sync with this
    ''' one (the two could disagree about whether the panel was shown).
    ''' </summary>
    Private Sub ToggleProjectExplorer()
        Try
            ' Toggle left panel visibility
            If pMainHPaned.Position > 0 Then
                ' Save current position and hide
                pLastLeftPanelWidth = pMainHPaned.Position

                ' Temporarily remove size request to allow position = 0
                If pLeftNotebook IsNot Nothing Then
                    pLeftNotebook.SetSizeRequest(-1, -1)
                End If

                pMainHPaned.Position = 0
                pLeftPanelVisible = False
                #If DEBUG Then
                Console.WriteLine($"Hidden left panel, saved width: {pLastLeftPanelWidth}")
                #End If
            Else
                ' Restore size request FIRST
                If pLeftNotebook IsNot Nothing Then
                    pLeftNotebook.SetSizeRequest(LEFT_PANEL_MINIMUM_WIDTH, -1)
                    pLeftNotebook.ShowAll() ' Ensure it's visible
                End If

                ' Then restore position
                Dim lRestoreWidth As Integer = If(pLastLeftPanelWidth > 0, pLastLeftPanelWidth, LEFT_PANEL_MINIMUM_WIDTH)

                ' Ensure it meets minimum
                If lRestoreWidth < LEFT_PANEL_MINIMUM_WIDTH Then
                    lRestoreWidth = LEFT_PANEL_MINIMUM_WIDTH
                End If

                pMainHPaned.Position = lRestoreWidth
                pLeftPanelVisible = True
                #If DEBUG Then
                Console.WriteLine($"Shown left panel at width: {lRestoreWidth}")
                #End If
            End If

            If pSettingsManager IsNot Nothing Then
                pSettingsManager.ShowProjectExplorer = pLeftPanelVisible
            End If
            UpdateMenuStates()
        Catch ex As Exception
            Console.WriteLine($"ToggleProjectExplorer error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub ShowQuickFix()
        #If DEBUG Then
        Console.WriteLine("Quick Fix - Not yet implemented")
        #End If
        ' TODO: Implement quick fix/show properties
    End Sub
    
    ''' <summary>
    ''' Shift+F12 - finds every whole-word occurrence of the symbol at the cursor across
    ''' every project in a loaded solution (or just the current project when none is
    ''' loaded), showing results in the existing Find Results panel/tab
    ''' </summary>
    Private Sub FindAllReferences()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor Is Nothing Then
                UpdateStatusBar("Find All References: No active editor")
                Return
            End If

            Dim lWord As String = lEditor.GetWordAtCursor()
            If String.IsNullOrWhiteSpace(lWord) Then
                UpdateStatusBar("Find All References: No symbol at cursor")
                Return
            End If

            If Not pBottomPanelVisible Then
                ToggleBottomPanel()
            End If

            If pBottomPanelManager IsNot Nothing AndAlso pFindPanel IsNot Nothing Then
                pBottomPanelManager.ShowTabForPanel(pFindPanel)
            End If

            pFindPanel?.FindAllReferences(lWord, pSolutionManager)

        Catch ex As Exception
            Console.WriteLine($"FindAllReferences error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Shift+F6 - prompts for a new name and renames every whole-word occurrence of the
    ''' symbol at the cursor across the current project, reusing the same project-wide
    ''' Replace All machinery the Find/Replace panel's own UI uses
    ''' (FindReplacePanel.RenameSymbol). This is a textual, whole-word rename - same
    ''' scope-blindness as FindAllReferences/Shift+F12 - not a semantically-aware one,
    ''' since there's no symbol table with reference tracking in this codebase
    ''' </summary>
    Private Sub RenameSymbol()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor Is Nothing Then
                UpdateStatusBar("Rename Symbol: No active editor")
                Return
            End If

            Dim lOldName As String = lEditor.GetWordAtCursor()
            If String.IsNullOrWhiteSpace(lOldName) Then
                UpdateStatusBar("Rename Symbol: No symbol at cursor")
                Return
            End If

            Using lInput As New InputDialog(Me, "Rename Symbol", $"Rename '{lOldName}' to:", lOldName, pThemeManager)
                If lInput.Run() = CInt(ResponseType.Ok) Then
                    Dim lNewName As String = lInput.Text.Trim()
                    If Not String.IsNullOrEmpty(lNewName) AndAlso lNewName <> lOldName Then
                        If Not pBottomPanelVisible Then
                            ToggleBottomPanel()
                        End If
                        If pBottomPanelManager IsNot Nothing AndAlso pFindPanel IsNot Nothing Then
                            pBottomPanelManager.ShowTabForPanel(pFindPanel)
                        End If
                        pFindPanel?.RenameSymbol(lOldName, lNewName)
                    End If
                End If
            End Using

        Catch ex As Exception
            Console.WriteLine($"RenameSymbol error: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' F8 - jumps to the next build error, wrapping to the first after the last. Delegates
    ''' to NavigateToNextError (MainWindow.FileManagement.vb), the real implementation
    ''' shared with the Build menu/toolbar
    ''' </summary>
    Private Sub NavigateToNextCompilationError()
        NavigateToNextError()
    End Sub
    
    Private Sub ToggleBreakpoint()
        #If DEBUG Then
        Console.WriteLine("Toggle Breakpoint - Not yet implemented")
        #End If
        ' TODO: Implement toggle breakpoint
    End Sub
    
    Private Sub StepOver()
        #If DEBUG Then
        Console.WriteLine("Step Over - Not yet implemented")
        #End If
        ' TODO: Implement step over debugging
    End Sub
    
    Private Sub StepOut()
        #If DEBUG Then
        Console.WriteLine("Step Out - Not yet implemented")
        #End If
        ' TODO: Implement step out debugging
    End Sub
    
    ''' <summary>
    ''' F12 - resolves the word at the current cursor position and routes it through the
    ''' same OnRequestGotoDefinition handler the editor's right-click "Go to Definition"
    ''' context-menu item uses (Editors/CustomDrawingEditor.ContextMenu.vb), so both entry
    ''' points share one implementation including cross-project resolution
    ''' </summary>
    Private Sub GoToDefinition()
        Try
            Dim lEditor As IEditor = GetCurrentEditor()
            If lEditor Is Nothing Then
                UpdateStatusBar("Go to Definition: No active editor")
                Return
            End If

            Dim lWord As String = lEditor.GetWordAtCursor()
            If String.IsNullOrWhiteSpace(lWord) Then
                UpdateStatusBar("Go to Definition: No symbol at cursor")
                Return
            End If

            Dim lEventArgs As New GotoDefinitionEventArgs() With {
                .FilePath = lEditor.FilePath,
                .LineNumber = lEditor.CurrentLine,
                .ColumnNumber = lEditor.CurrentColumn,
                .Word = lWord
            }

            OnRequestGotoDefinition(lEditor, lEventArgs)

        Catch ex As Exception
            Console.WriteLine($"GoToDefinition error: {ex.Message}")
        End Try
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
            #If DEBUG Then
            Console.WriteLine("OnBuildProject called (F6 - Build Only)")
            #End If
            
            ' Check if already building
            If pIsBuildingNow Then
                #If DEBUG Then
                Console.WriteLine("OnBuildProject: Already building (pIsBuildingNow check)")
                #End If
                ShowInfo("Build in Progress", "A build is already in progress.")
                Return
            End If
            
            ' Check if already building using BuildManager
            If pBuildManager IsNot Nothing AndAlso pBuildManager.IsBuilding Then
                #If DEBUG Then
                Console.WriteLine("OnBuildProject: Build already in progress (BuildManager check)")
                #End If
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
        lText.AppendLine("  Ctrl+P          Quick Open (Go to File)")
        lText.AppendLine("  Shift+F6        Rename Symbol")
        lText.AppendLine("  F8              Next Build Error")
        lText.AppendLine("  Shift+F8        Previous Build Error")
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
        lText.AppendLine("      Not Redo. Use Ctrl+R Or Ctrl+Shift+Z for Redo.")
        
        Return lText.ToString()
    End Function

End Class