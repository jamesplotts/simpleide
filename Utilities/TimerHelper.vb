' TimerHelper.vb
' Created: 2025-08-19 10:58:55

Imports System

Namespace Utilities
    
    ''' <summary>
    ''' Helper class for safe GLib timer management
    ''' </summary>
    Public Class TimerHelper
        
        ''' <summary>
        ''' Safely removes a GLib timer source
        ''' </summary>
        ''' <param name="vTimerId">The timer ID to remove</param>
        ''' <returns>True if removed successfully, False if already removed</returns>
        Public Shared Function SafeRemove(ByRef vTimerId As UInteger) As Boolean
            Try
                If vTimerId = 0 Then Return False
                
                Dim lId As UInteger = vTimerId
                vTimerId = 0  ' Clear immediately
                
                Try
                    GLib.Source.Remove(lId)
                    Return True
                Catch
                    ' Timer already removed
                    Return False
                End Try
                
            Catch ex As Exception
                Console.WriteLine($"TimerHelper.SafeRemove error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Safely adds a new timeout, removing any existing one
        ''' </summary>
        ''' <param name="vTimerId">Timer ID variable (will be updated)</param>
        ''' <param name="vInterval">Interval in milliseconds</param>
        ''' <param name="vCallback">Callback function</param>
        Public Shared Sub SafeAddTimeout(ByRef vTimerId As UInteger, vInterval As UInteger, vCallback As GLib.TimeoutHandler)
            Try
                ' Remove existing timer if any
                SafeRemove(vTimerId)
                
                ' Add new timer
                vTimerId = GLib.Timeout.Add(vInterval, vCallback)
                
            Catch ex As Exception
                Console.WriteLine($"TimerHelper.SafeAddTimeout error: {ex.Message}")
                vTimerId = 0
            End Try
        End Sub
        
    End Class
    
End Namespace
