' CustomDrawProjectExplorer.Scale.vb
' Created: 2025-08-18 07:28:29
Imports Gtk
Imports Gdk
Imports Cairo
Imports Pango
Imports System
Imports System.IO
Imports System.Xml
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Utilities

Namespace Widgets
    
    ''' <summary>
    ''' Custom drawn implementation of the Project Explorer providing file tree view
    ''' </summary>
    ''' <remarks>
    ''' Provides a custom-rendered tree view of project files with viewport culling,
    ''' unified scaling support with Object Explorer, and comprehensive file management
    ''' </remarks>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box

        ''' <summary>
        ''' Increases the scale by one step (25%)
        ''' </summary>
        Private Sub IncreaseScale()
            Try
                Dim lNewScale As Integer = pCurrentScale + 25
                If lNewScale > MAX_SCALE Then lNewScale = MAX_SCALE
                
                If lNewScale <> pCurrentScale Then
                    ApplyScale(lNewScale)
                    SaveUnifiedTextScale(lNewScale)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"IncreaseScale error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Decreases the scale by one step (25%)
        ''' </summary>
        Private Sub DecreaseScale()
            Try
                Dim lNewScale As Integer = pCurrentScale - 25
                If lNewScale < MIN_SCALE Then lNewScale = MIN_SCALE
                
                If lNewScale <> pCurrentScale Then
                    ApplyScale(lNewScale)
                    SaveUnifiedTextScale(lNewScale)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"DecreaseScale error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Resets the scale to default (100%)
        ''' </summary>
        Private Sub ResetScale()
            Try
                If pCurrentScale <> DEFAULT_SCALE Then
                    ApplyScale(DEFAULT_SCALE)
                    SaveUnifiedTextScale(DEFAULT_SCALE)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ResetScale error: {ex.Message}")
            End Try
        End Sub


    End Class

End Namespace
