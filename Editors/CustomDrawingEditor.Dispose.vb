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


        ' Replace: SimpleIDE.Editors.CustomDrawingEditor.Dispose
        Protected Overrides Sub Dispose(vDisposing As Boolean)
            Try
                ' Only dispose once
                If pIsDisposed Then 
                    MyBase.Dispose(vDisposing)
                    Return
                End If
                
                If vDisposing Then
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
        
                    ' Unsubscribe from ProjectManager
                    If pProjectManager IsNot Nothing Then
                        RemoveHandler pProjectManager.ParseCompleted, AddressOf OnProjectManagerParseCompleted
                        RemoveHandler pProjectManager.IdentifierMapUpdated, AddressOf OnProjectManagerIdentifierMapUpdated
                        RemoveHandler pProjectManager.ProjectClosed, AddressOf OnProjectManagerProjectClosed
                        pProjectManager = Nothing
                    End If
                    
                    ' CRITICAL FIX: Properly unhook ALL SourceFileInfo events
                    ' This was missing and causing the reopening issue!
                    If pSourceFileInfo IsNot Nothing Then
                        ' Remove ContentChanged handler
                        RemoveHandler pSourceFileInfo.ContentChanged, AddressOf OnSourceFileContentChanged
                        
                        ' CRITICAL: Call UnhookSourceFileInfoEvents to remove TextLinesChanged and RenderingChanged handlers
                        UnhookSourceFileInfoEvents()
                        
                        ' Clear the reference
                        pSourceFileInfo = Nothing
                    End If
        
                    ' Clear collections
                    pSearchMatches?.Clear()
                    pIdentifierCaseMap?.Clear()
                    
                    ' Clean up GTK resources
                    If pDrawingArea IsNot Nothing Then
                        ' Remove all event handlers using stored delegates
                        ' Create local delegates to ensure proper type matching
                        Dim lDrawnHandler As DrawnHandler = AddressOf OnDrawn
                        Dim lKeyPressHandler As KeyPressEventHandler = AddressOf OnKeyPress
                        Dim lKeyReleaseHandler As KeyReleaseEventHandler = AddressOf OnKeyRelease
                        Dim lButtonPressHandler As ButtonPressEventHandler = AddressOf OnButtonPress
                        Dim lButtonReleaseHandler As ButtonReleaseEventHandler = AddressOf OnButtonRelease
                        Dim lMotionNotifyHandler As MotionNotifyEventHandler = AddressOf OnMotionNotify
                        Dim lScrollHandler As ScrollEventHandler = AddressOf OnScrollEvent
                        Dim lFocusInHandler As FocusInEventHandler = AddressOf OnFocusIn
                        Dim lFocusOutHandler As FocusOutEventHandler = AddressOf OnFocusOut
                        
                        RemoveHandler pDrawingArea.Drawn, lDrawnHandler
                        RemoveHandler pDrawingArea.KeyPressEvent, lKeyPressHandler
                        RemoveHandler pDrawingArea.KeyReleaseEvent, lKeyReleaseHandler
                        RemoveHandler pDrawingArea.ButtonPressEvent, lButtonPressHandler
                        RemoveHandler pDrawingArea.ButtonReleaseEvent, lButtonReleaseHandler
                        RemoveHandler pDrawingArea.MotionNotifyEvent, lMotionNotifyHandler
                        RemoveHandler pDrawingArea.ScrollEvent, lScrollHandler
                        RemoveHandler pDrawingArea.FocusInEvent, lFocusInHandler
                        RemoveHandler pDrawingArea.FocusOutEvent, lFocusOutHandler
                        
                        pDrawingArea.Destroy()
                        pDrawingArea = Nothing
                    End If
        
                    If pVScrollbar IsNot Nothing AndAlso pVScrollbarHandler IsNot Nothing Then
                        RemoveHandler pVScrollbar.ValueChanged, pVScrollbarHandler
                    End If
                    
                    If pHScrollbar IsNot Nothing AndAlso pHScrollbarHandler IsNot Nothing Then
                        RemoveHandler pHScrollbar.ValueChanged, pHScrollbarHandler
                    End If
                    
                    ' Clear references
                    pRootNode = Nothing
                    pFilePath = Nothing
        
                    ' Dispose widgets
                    pLineNumberWidget?.Destroy()
                    pVScrollbar?.Destroy()
                    pHScrollbar?.Destroy()
                    pCornerBox?.Destroy()
                    pMainGrid?.Destroy()
                    
                    ' Dispose context menus
                    pContextMenu?.Destroy()
                    pLineNumberContextMenu?.Destroy()
                    
                    ' Clear references
                    pLineNumberWidget = Nothing
                    pDrawingArea = Nothing
                    pVScrollbar = Nothing
                    pHScrollbar = Nothing
                    pCornerBox = Nothing
                    pMainGrid = Nothing
                    
                    Console.WriteLine("CustomDrawingEditor disposed")
                End If
                
                ' Mark as disposed before calling base
                pIsDisposed = True
                
                ' Call base dispose with the correct parameter
                MyBase.Dispose(vDisposing)
                
            Catch ex As Exception
                Console.WriteLine($"CustomDrawingEditor.Dispose error: {ex.Message}")
                ' Still call base even if error occurred
                MyBase.Dispose(vDisposing)
            End Try
        End Sub

        

        
    End Class
    
End Namespace
