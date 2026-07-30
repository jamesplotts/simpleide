' CustomDrawNotebook.Animation.vb - Animation support for custom notebook
Imports Gtk
Imports GLib
Imports System
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    Partial Public Class CustomDrawNotebook
        
        ' Animation duration in milliseconds
        Private Const ANIMATION_DURATION As Integer = 200
        Private Const ANIMATION_FPS As Integer = 60
        
        ''' <summary>
        ''' Animates scrolling to a target offset
        ''' </summary>
        ''' <param name="vTargetOffset">Target scroll offset</param>
        Private Sub AnimateScrollTo(vTargetOffset As Integer)
            Try
                ' Clamp target to valid range
                vTargetOffset = Math.Max(0, Math.Min(pMaxScrollOffset, vTargetOffset))
                
                ' Skip if already at target
                If vTargetOffset = pScrollOffset Then Return
                
                ' Stop any existing animation
                If pAnimationTimer <> 0 Then
                    GLib.Source.Remove(pAnimationTimer)
                    pAnimationTimer = 0
                End If
                
                ' Setup animation parameters
                pAnimationStartOffset = pScrollOffset
                pAnimationTargetOffset = vTargetOffset
                pAnimationProgress = 0
                
                ' Start animation timer
                pAnimationTimer = GLib.Timeout.Add(CUInt(1000 \ ANIMATION_FPS), AddressOf OnAnimationTick)
                
            Catch ex As Exception
                Console.WriteLine($"AnimateScrollTo error: {ex.Message}")
                ' Fallback to immediate scroll
                pScrollOffset = vTargetOffset
                UpdateScrollButtons()
                pTabBar.QueueDraw()
            End Try
        End Sub
        
        ''' <summary>
        ''' Animation timer tick handler
        ''' </summary>
        ''' <returns>True to continue animation, False to stop</returns>
        Private Function OnAnimationTick() As Boolean
            Try
                ' Update progress
                pAnimationProgress += (1000.0 / ANIMATION_FPS) / ANIMATION_DURATION
                
                If pAnimationProgress >= 1.0 Then
                    ' Animation complete
                    pScrollOffset = pAnimationTargetOffset
                    pAnimationTimer = 0
                    UpdateNavigationButtons()
                    pTabBar.QueueDraw()
                    Return False
                Else
                    ' Interpolate position using ease-out curve
                    Dim lEasedProgress As Double = EaseOutCubic(pAnimationProgress)
                    pScrollOffset = CInt(pAnimationStartOffset + 
                                       (pAnimationTargetOffset - pAnimationStartOffset) * lEasedProgress)
                    UpdateNavigationButtons()
                    pTabBar.QueueDraw()
                    Return True
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnAnimationTick error: {ex.Message}")
                pAnimationTimer = 0
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Ease-out cubic interpolation for smooth animation
        ''' </summary>
        ''' <param name="vT">Progress from 0 to 1</param>
        ''' <returns>Eased value from 0 to 1</returns>
        Private Function EaseOutCubic(vT As Double) As Double
            Dim lT As Double = vT - 1
            Return lT * lT * lT + 1
        End Function
        
        ''' <summary>
        ''' Stops any running animation
        ''' </summary>
        Private Sub StopAnimation()
            Try
                If pAnimationTimer <> 0 Then
                    GLib.Source.Remove(pAnimationTimer)
                    pAnimationTimer = 0
                End If
                
            Catch ex As Exception
                Console.WriteLine($"StopAnimation error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Cleans up animation resources when disposing
        ''' </summary>
        Protected Overrides Sub Dispose(vDisposing As Boolean)
            Try
                If vDisposing Then
                    StopAnimation()
                End If
                
                MyBase.Dispose(vDisposing)
                
            Catch ex As Exception
                Console.WriteLine($"Dispose error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace