' Editors/CustomDrawingEditor.Dispose.vb - Disposal implementation
Imports System
Imports Gtk
Imports Gdk
Imports SimpleIDE.Interfaces

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
                
                ' Register all event handlers
                If pDrawingArea IsNot Nothing Then
                    AddHandler pDrawingArea.Drawn, pDrawnHandler
                    AddHandler pDrawingArea.KeyPressEvent, pKeyPressHandler
                    AddHandler pDrawingArea.KeyReleaseEvent, pKeyReleaseHandler
                    AddHandler pDrawingArea.ButtonPressEvent, pButtonPressHandler
                    AddHandler pDrawingArea.ButtonReleaseEvent, pButtonReleaseHandler
                    AddHandler pDrawingArea.MotionNotifyEvent, pMotionNotifyHandler
                    AddHandler pDrawingArea.ScrollEvent, pScrollHandler
                    AddHandler pLineNumberArea.Drawn, AddressOf OnLineNumberAreaDraw
                End If
                
                ' LINE NUMBER AREA EVENT HANDLERS:
                If pLineNumberArea IsNot Nothing Then
                    AddHandler pLineNumberArea.Drawn, AddressOf OnLineNumberAreaDraw
                    AddHandler pLineNumberArea.ButtonPressEvent, AddressOf OnLineNumberButtonPress
                    AddHandler pLineNumberArea.MotionNotifyEvent, AddressOf OnLineNumberMotionNotify
                    AddHandler pLineNumberArea.ButtonReleaseEvent, AddressOf OnLineNumberButtonRelease
                    
                    ' Make sure the line number area can receive mouse events
                    pLineNumberArea.AddEvents(CInt(EventMask.ButtonPressMask Or EventMask.ButtonReleaseMask Or EventMask.PointerMotionMask))
                    
                    Console.WriteLine("Line number area mouse events registered")
                End If
                
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
        
        ' ===== IDisposable Implementation =====
        
        Protected Overrides Sub Dispose(vDisposing As Boolean)
            Try
                If vDisposing Then
                    ' Clean up managed resources
                    CleanupResources()
                End If
                
                MyBase.Dispose(vDisposing)
                
            Catch ex As Exception
                Console.WriteLine($"Dispose error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CleanupResources()
            Try
                ' Stop timers
                If pCursorBlinkTimer <> 0 Then
                    Dim lTimerId As UInteger = pCursorBlinkTimer
                    pCursorBlinkTimer = 0  ' Clear BEFORE removing
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch
                        ' Timer may have already expired - this is OK
                    End Try
                End If
                
                If pParseTimer <> 0 Then
                    Dim lTimerId As UInteger = pParseTimer
                    pParseTimer = 0  ' Clear BEFORE removing
                    Try
                        GLib.Source.Remove(lTimerId)
                    Catch
                        ' Timer may have already expired - this is OK
                    End Try
                End If
                
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
                End If
                
                ' Unhook scrollbar event handlers
                If pVScrollbar IsNot Nothing AndAlso pVScrollbarHandler IsNot Nothing Then
                    RemoveHandler pVScrollbar.ValueChanged, pVScrollbarHandler
                End If
                
                If pHScrollbar IsNot Nothing AndAlso pHScrollbarHandler IsNot Nothing Then
                    RemoveHandler pHScrollbar.ValueChanged, pHScrollbarHandler
                End If
                pThemeManager = Nothing
                
                ' Clear delegate references
                pDrawnHandler = Nothing
                pKeyPressHandler = Nothing
                pKeyReleaseHandler = Nothing
                pButtonPressHandler = Nothing
                pButtonReleaseHandler = Nothing
                pMotionNotifyHandler = Nothing
                pScrollHandler = Nothing
                pVScrollbarHandler = Nothing
                pHScrollbarHandler = Nothing
                
            Catch ex As Exception
                Console.WriteLine($"CleanupResources error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
