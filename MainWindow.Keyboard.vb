 
' MainWindow.Keyboard.vb - Keyboard shortcuts and accelerators
Imports Gtk
Imports Gdk
Imports System
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces

Partial Public Class MainWindow
    
    ' Setup keyboard accelerators
    Private Sub SetupKeyboardShortcuts()
        Try
            Dim lAccelGroup As New AccelGroup()
            AddAccelGroup(lAccelGroup)
            
            ' File shortcuts
            AddAccelerator(lAccelGroup, "N", ModifierType.ControlMask, AddressOf OnNewFile)
            AddAccelerator(lAccelGroup, "O", ModifierType.ControlMask, AddressOf OnOpenFile)
            AddAccelerator(lAccelGroup, "S", ModifierType.ControlMask, AddressOf OnSave)
            AddAccelerator(lAccelGroup, "S", ModifierType.ControlMask Or ModifierType.ShiftMask, AddressOf OnSaveAll)
            AddAccelerator(lAccelGroup, "W", ModifierType.ControlMask, AddressOf OnCloseFile)
            AddAccelerator(lAccelGroup, "Q", ModifierType.ControlMask, AddressOf OnQuit)
            
            ' Edit shortcuts
            AddAccelerator(lAccelGroup, "Z", ModifierType.ControlMask, AddressOf OnUndo)
            AddAccelerator(lAccelGroup, "y", ModifierType.ControlMask, AddressOf OnRedo)
            AddAccelerator(lAccelGroup, "x", ModifierType.ControlMask, AddressOf OnCut)
            AddAccelerator(lAccelGroup, "C", ModifierType.ControlMask, AddressOf OnCopy)
            AddAccelerator(lAccelGroup, "V", ModifierType.ControlMask, AddressOf OnPaste)
            AddAccelerator(lAccelGroup, "A", ModifierType.ControlMask, AddressOf OnSelectAll)
            AddAccelerator(lAccelGroup, "F", ModifierType.ControlMask, AddressOf ShowFindPanel)
            AddAccelerator(lAccelGroup, "H", ModifierType.ControlMask, AddressOf ShowFindPanel)
            ' TODO: AddAccelerator(lAccelGroup, "F", ModifierType.ControlMask Or ModifierType.ShiftMask, AddressOf OnFindInFiles)
            'AddAccelerator(lAccelGroup, "G", ModifierType.ControlMask, AddressOf GotoLine)
            AddAccelerator(lAccelGroup, "slash", ModifierType.ControlMask, AddressOf OnToggleComment)
            
            ' Build shortcuts
            AddAccelerator(lAccelGroup, "F5", ModifierType.None, AddressOf OnBuildProject)
            AddAccelerator(lAccelGroup, "F6", ModifierType.None, AddressOf OnBuildAndRun)
            ' TODO: AddAccelerator(lAccelGroup, "F5", ModifierType.ShiftMask, AddressOf OnStopDebugging)
            
            ' View shortcuts
            AddAccelerator(lAccelGroup, "F11", ModifierType.None, AddressOf OnToggleFullScreen)
            ' TODO: AddAccelerator(lAccelGroup, "plus", ModifierType.ControlMask, AddressOf OnZoomIn)
            ' TODO: AddAccelerator(lAccelGroup, "minus", ModifierType.ControlMask, AddressOf OnZoomOut)
            ' TODO: AddAccelerator(lAccelGroup, "0", ModifierType.ControlMask, AddressOf OnZoomReset)
            
            ' Help
            AddAccelerator(lAccelGroup, "F1", ModifierType.None, AddressOf ShowHelpPanel)
            
        Catch ex As Exception
            Console.WriteLine($"SetupKeyboardShortcuts error: {ex.Message}")
        End Try
    End Sub
    
    ' Store handlers for accelerators
    Private pAccelHandlers As New Dictionary(Of String, EventHandler)

    Private Shadows Sub AddAccelerator(vAccelGroup As AccelGroup, vKey As String, vModifiers As ModifierType, vHandler As EventHandler)
        Try
            Dim lKeyval As UInteger = Keyval.FromName(vKey)
            If lKeyval <> 0 Then
                ' Store the handler with a unique key
                Dim lHandlerKey As String = $"{lKeyval}_{CInt(vModifiers)}"
                pAccelHandlers(lHandlerKey) = vHandler
                
                ' Use AddHandler syntax with a method reference, not lambda
                AddHandler vAccelGroup.AccelActivate, 
                    Sub(sender As Object, args As AccelActivateArgs)
                        OnAccelGroupActivated(sender, args, lHandlerKey)
                    End Sub
                
                ' Add the accelerator to the group
                vAccelGroup.Connect(lKeyval, vModifiers, AccelFlags.Visible, Nothing)
            End If
        Catch ex As Exception
            Console.WriteLine($"AddAccelerator error for key '{vKey}': {ex.Message}")
        End Try
    End Sub
    
    ' Handle accelerator activation
    Private Sub OnAccelGroupActivated(vSender As Object, vArgs As AccelActivateArgs, vHandlerKey As String)
        Try
            If pAccelHandlers.ContainsKey(vHandlerKey) Then
                Dim lHandler As EventHandler = pAccelHandlers(vHandlerKey)
                lHandler.Invoke(Me, EventArgs.Empty)
            End If
        Catch ex As Exception
            Console.WriteLine($"Accelerator handler error: {ex.Message}")
        End Try
    End Sub
    
    ' Global key press handler
    Private Sub OnWindowKeyPress(vSender As Object, vArgs As KeyPressEventArgs)
        Try
            ' Check for special key combinations
            Dim lKey As Gdk.key = CType(vArgs.Event.key, Gdk.key)
            Dim lModifiers As ModifierType = vArgs.Event.State
            Console.WriteLine("Keypress")
            
                

            ' Tab navigation
            If lModifiers.HasFlag(ModifierType.ControlMask) Then
                Select Case lKey
                    Case Gdk.key.Tab
                        ' Ctrl+Tab - Next tab
                        If pNotebook.CurrentPage < pNotebook.NPages - 1 Then
                            pNotebook.CurrentPage += 1
                        Else
                            pNotebook.CurrentPage = 0
                        End If
                        vArgs.RetVal = True
                        Return
                        
                    Case Gdk.key.ISO_Left_Tab ' Shift+Tab
                        ' Ctrl+Shift+Tab - Previous tab
                        If pNotebook.CurrentPage > 0 Then
                            pNotebook.CurrentPage -= 1
                        Else
                            pNotebook.CurrentPage = pNotebook.NPages - 1
                        End If
                        vArgs.RetVal = True
                        Return
                        
                    Case Gdk.key.Key_1 To Gdk.key.Key_9
                        ' Ctrl+1-9 - Switch to specific tab
                        Dim lTabIndex As Integer = CInt(lKey) - CInt(Gdk.key.Key_1)
                        If lTabIndex < pNotebook.NPages Then
                            pNotebook.CurrentPage = lTabIndex
                            vArgs.RetVal = True
                            Return
                        End If
                End Select
            End If
            
            ' Escape key handling
            If lKey = Gdk.key.Escape Then
                ' Close bottom panel if visible
                If pBottomPanelVisible Then
                    HideBottomPanel()
                    vArgs.RetVal = True
                    Return
                End If
                
                ' Cancel IntelliSense
                Dim lCurrentTab As Models.TabInfo = GetCurrentTabInfo()
                If lCurrentTab IsNot Nothing AndAlso lCurrentTab.Editor IsNot Nothing Then
                    ' Try to cancel IntelliSense if the editor supports it
                    Try
                        ' Check if editor has CancelIntelliSense method
                        Dim lEditor As IEditor = lCurrentTab.Editor
                        If TypeOf lEditor Is CustomDrawingEditor Then
                            DirectCast(lEditor, CustomDrawingEditor).CancelIntelliSense()
                        End If
                    Catch ex As Exception
                        ' IntelliSense cancellation failed, but continue
                        Console.WriteLine($"Failed to cancel IntelliSense: {ex.Message}")
                    End Try
                End If
            End If
            
            ' F4 - Navigate to next error
            If lKey = Gdk.key.F4 Then
                NavigateToNextError()
                vArgs.RetVal = True
                Return
            End If
            
            ' Shift+F4 - Navigate to previous error
            If lKey = Gdk.key.F4 AndAlso lModifiers.HasFlag(ModifierType.ShiftMask) Then
                NavigateToPreviousError()
                vArgs.RetVal = True
                Return
            End If
            
        Catch ex As Exception
            Console.WriteLine($"OnWindowKeyPress error: {ex.Message}")
        End Try
    End Sub
    
    ' Navigation helpers
    Private Sub NavigateToNextError()
        Try
            ' Get current selection in error list
            Dim lSelection As TreeSelection = pErrorListView.Selection
            Dim lModel As ITreeModel = Nothing
            Dim lIter As TreeIter = Nothing
            
            If lSelection.GetSelected(lModel, lIter) Then
                ' Move to next
                If lModel.IterNext(lIter) Then
                    lSelection.SelectIter(lIter)
                    pErrorListView.ActivateRow(lModel.GetPath(lIter), pErrorListView.Columns(0))
                End If
            Else
                ' Select first error
                If lModel.GetIterFirst(lIter) Then
                    lSelection.SelectIter(lIter)
                    pErrorListView.ActivateRow(lModel.GetPath(lIter), pErrorListView.Columns(0))
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"NavigateToNextError error: {ex.Message}")
        End Try
    End Sub
    
    Private Sub NavigateToPreviousError()
        Try
            ' Get current selection in error list
            Dim lSelection As TreeSelection = pErrorListView.Selection
            Dim lModel As ITreeModel = Nothing
            Dim lIter As TreeIter = Nothing
            
            If lSelection.GetSelected(lModel, lIter) Then
                ' Move to previous
                Dim lPath As TreePath = lModel.GetPath(lIter)
                If lPath.Prev() Then
                    lModel.GetIter(lIter, lPath)
                    lSelection.SelectIter(lIter)
                    pErrorListView.ActivateRow(lPath, pErrorListView.Columns(0))
                End If
            End If
            
        Catch ex As Exception
            Console.WriteLine($"NavigateToPreviousError error: {ex.Message}")
        End Try
    End Sub
    
End Class
