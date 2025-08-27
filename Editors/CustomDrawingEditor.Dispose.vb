' Editors/CustomDrawingEditor.Dispose.vb - Disposal implementation
Imports System
Imports Gtk
Imports Gdk
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Managers

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        Private pIsDisposed As Boolean
        
        ' Store delegates to ensure proper removal
        Private pDrawnHandler As DrawnHandler
        Private pKeyPressHandler As KeyPressEventHandler
        Private pKeyReleaseHandler As KeyReleaseEventHandler
        Private pButtonPressHandler As ButtonPressEventHandler
        Private pButtonReleaseHandler As ButtonReleaseEventHandler
        Private pMotionNotifyHandler As MotionNotifyEventHandler
        Private pScrollHandler As ScrollEventHandler
        Private pVScrollbarHandler As EventHandler
        Private pHScrollbarHandler As EventHandler
        
        ' ===== Event Handler Registration (called from constructor) =====

        ''' <summary>
        ''' Registers all event handlers for the editor components
        ''' </summary>
        Private Sub RegisterEventHandlers()
            Try
                ' Create and store delegates
                pDrawnHandler = New DrawnHandler(AddressOf OnDrawn)
                pKeyPressHandler = New KeyPressEventHandler(AddressOf OnKeyPress)
                pKeyReleaseHandler = New KeyReleaseEventHandler(AddressOf OnKeyRelease)
                pButtonPressHandler = New ButtonPressEventHandler(AddressOf OnButtonPress)
                pButtonReleaseHandler = New ButtonReleaseEventHandler(AddressOf OnButtonRelease)
                pMotionNotifyHandler = New MotionNotifyEventHandler(AddressOf OnMotionNotify)
                pScrollHandler = New ScrollEventHandler(AddressOf OnScrollEvent)
                pVScrollbarHandler = New EventHandler(AddressOf OnVScrollbarValueChanged)
                pHScrollbarHandler = New EventHandler(AddressOf OnHScrollbarValueChanged)
                
                ' Register DRAWING AREA event handlers
                If pDrawingArea IsNot Nothing Then
                    AddHandler pDrawingArea.Drawn, pDrawnHandler
                    AddHandler pDrawingArea.KeyPressEvent, pKeyPressHandler
                    AddHandler pDrawingArea.KeyReleaseEvent, pKeyReleaseHandler
                    AddHandler pDrawingArea.ButtonPressEvent, pButtonPressHandler
                    AddHandler pDrawingArea.ButtonReleaseEvent, pButtonReleaseHandler
                    AddHandler pDrawingArea.MotionNotifyEvent, pMotionNotifyHandler
                    AddHandler pDrawingArea.ScrollEvent, pScrollHandler
                End If
                
                ' NOTE: Line number widget (pLineNumberWidget) handles its own events internally
                ' No need to register events here - they're handled in LineNumberWidget constructor
                
                ' Register scrollbar handlers
                If pVScrollbar IsNot Nothing Then
                    AddHandler pVScrollbar.ValueChanged, pVScrollbarHandler
                End If
                
                If pHScrollbar IsNot Nothing Then
                    AddHandler pHScrollbar.ValueChanged, pHScrollbarHandler
                End If
                
            Catch ex As Exception
                Console.WriteLine($"RegisterEventHandlers error: {ex.Message}")
            End Try
        End Sub

        Public Property ShowLineNumbers As Boolean Implements IEditor.ShowLineNumbers
            Get
                Return pShowLineNumbers
            End Get
            Set(value As Boolean)
                Try
                    If pShowLineNumbers <> value Then
                        pShowLineNumbers = value
                        
                        ' Show/hide line number widget
                        If pLineNumberWidget IsNot Nothing Then
                            pLineNumberWidget.Visible = pShowLineNumbers
                        End If
                        
                        ' Queue redraw
                        pDrawingArea?.QueueDraw()
                    End If
                Catch ex As Exception
                    Console.WriteLine($"ShowLineNumbers setter error: {ex.Message}")
                End Try
            End Set
        End Property

        Protected Overrides Sub Dispose(vDisposing As Boolean)
            Try
                If vDisposing AndAlso Not pIsDisposed Then
                    ' Remove timer
                    If pCursorBlinkTimer > 0 Then
                        GLib.Source.Remove(pCursorBlinkTimer)
                        pCursorBlinkTimer = 0
                    End If
                    
                    ' Remove event handlers
                    If pDrawingArea IsNot Nothing Then
                        If pDrawnHandler IsNot Nothing Then RemoveHandler pDrawingArea.Drawn, pDrawnHandler
                        If pKeyPressHandler IsNot Nothing Then RemoveHandler pDrawingArea.KeyPressEvent, pKeyPressHandler
                        If pKeyReleaseHandler IsNot Nothing Then RemoveHandler pDrawingArea.KeyReleaseEvent, pKeyReleaseHandler
                        If pButtonPressHandler IsNot Nothing Then RemoveHandler pDrawingArea.ButtonPressEvent, pButtonPressHandler
                        If pButtonReleaseHandler IsNot Nothing Then RemoveHandler pDrawingArea.ButtonReleaseEvent, pButtonReleaseHandler
                        If pMotionNotifyHandler IsNot Nothing Then RemoveHandler pDrawingArea.MotionNotifyEvent, pMotionNotifyHandler
                        If pScrollHandler IsNot Nothing Then RemoveHandler pDrawingArea.ScrollEvent, pScrollHandler
                    End If
                    
                    ' LineNumberWidget handles its own disposal
                    
                    If pVScrollbar IsNot Nothing AndAlso pVScrollbarHandler IsNot Nothing Then
                        RemoveHandler pVScrollbar.ValueChanged, pVScrollbarHandler
                    End If
                    
                    If pHScrollbar IsNot Nothing AndAlso pHScrollbarHandler IsNot Nothing Then
                        RemoveHandler pHScrollbar.ValueChanged, pHScrollbarHandler
                    End If
                    
                    ' Dispose widgets
                    pLineNumberWidget?.Dispose()
                    pDrawingArea?.Dispose()
                    pVScrollbar?.Dispose()
                    pHScrollbar?.Dispose()
                    pCornerBox?.Dispose()
                    pMainGrid?.Dispose()
                    
                    ' Dispose context menus
                    pContextMenu?.Dispose()
                    pLineNumberContextMenu?.Dispose()
                    
                    ' Clear references
                    pLineNumberWidget = Nothing
                    pDrawingArea = Nothing
                    pVScrollbar = Nothing
                    pHScrollbar = Nothing
                    pCornerBox = Nothing
                    pMainGrid = Nothing
                    
                    pIsDisposed = True
                End If
            Catch ex As Exception
                Console.WriteLine($"Dispose error: {ex.Message}")
            End Try
            
            MyBase.Dispose(vDisposing)
        End Sub
        
        ' ===== IDisposable Implementation =====
        
        ''' <summary>
        ''' Clean up resources when disposing
        ''' </summary>
        ''' <remarks>
        ''' Updated to remove parse timer cleanup since we no longer use local parsing
        ''' </remarks>
        Private Sub CleanupResources()
            Try
                ' Stop cursor blink timer
                If pCursorBlinkTimer <> 0 Then
                    Dim lTimerId As UInteger = pCursorBlinkTimer
                    pCursorBlinkTimer = 0  ' Clear BEFORE removing
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch
                        ' Timer may have already expired - this is OK
                    End Try
                End If
                
                ' REMOVED: pParseTimer cleanup - no longer used with centralized parsing
                
                ' Unsubscribe from ProjectManager parse events
                Try
                    Dim lMainWindow As MainWindow = TryCast(Me.Toplevel, MainWindow)
                    If lMainWindow IsNot Nothing Then
                        Dim lProjectManager As ProjectManager = ProjectManager
                        If lProjectManager IsNot Nothing Then
                            RemoveHandler lProjectManager.ParseCompleted, AddressOf OnProjectParseCompleted
                        End If
                    End If
                Catch
                    ' Ignore errors during cleanup
                End Try
                
                ' Unhook event handlers using stored delegates
                If pDrawingArea IsNot Nothing Then
                    If pDrawnHandler IsNot Nothing Then
                        RemoveHandler pDrawingArea.Drawn, pDrawnHandler
                    End If
                    If pKeyPressHandler IsNot Nothing Then
                        RemoveHandler pDrawingArea.KeyPressEvent, pKeyPressHandler
                    End If
                    If pKeyReleaseHandler IsNot Nothing Then
                        RemoveHandler pDrawingArea.KeyReleaseEvent, pKeyReleaseHandler
                    End If
                    If pButtonPressHandler IsNot Nothing Then
                        RemoveHandler pDrawingArea.ButtonPressEvent, pButtonPressHandler
                    End If
                    If pButtonReleaseHandler IsNot Nothing Then
                        RemoveHandler pDrawingArea.ButtonReleaseEvent, pButtonReleaseHandler
                    End If
                    If pMotionNotifyHandler IsNot Nothing Then
                        RemoveHandler pDrawingArea.MotionNotifyEvent, pMotionNotifyHandler
                    End If
                    If pScrollHandler IsNot Nothing Then
                        RemoveHandler pDrawingArea.ScrollEvent, pScrollHandler
                    End If
                    If pFocusInHandler IsNot Nothing Then
                        RemoveHandler pDrawingArea.FocusInEvent, pFocusInHandler
                    End If
                    If pFocusOutHandler IsNot Nothing Then
                        RemoveHandler pDrawingArea.FocusOutEvent, pFocusOutHandler
                    End If
                End If
                
                ' Unhook scrollbar handlers
                If pVScrollbar IsNot Nothing AndAlso pVScrollValueChangedHandler IsNot Nothing Then
                    RemoveHandler pVScrollbar.ValueChanged, pVScrollValueChangedHandler
                End If
                
                If pHScrollbar IsNot Nothing AndAlso pHScrollValueChangedHandler IsNot Nothing Then
                    RemoveHandler pHScrollbar.ValueChanged, pHScrollValueChangedHandler
                End If
                
                ' Clear collections
                pSearchMatches?.Clear()
                pIdentifierCaseMap?.Clear()
                
                ' Clear arrays
                pLineMetadata = Nothing
                pCharacterColors = Nothing
                
            Catch ex As Exception
                Console.WriteLine($"CleanupResources error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
