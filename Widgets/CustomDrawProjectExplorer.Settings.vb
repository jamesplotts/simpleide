' Widgets/CustomDrawProjectExplorer.Settings.vb - Settings management
' Created: 2025-08-17
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing settings management functionality
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Settings Loading and Saving =====
        
        ''' <summary>
        ''' Loads settings from the settings manager
        ''' </summary>
        Private Sub LoadSettings()
            Try
                ' Load unified text scale (shared with Object Explorer)
                pCurrentScale = pSettingsManager.GetInteger("Explorer.TextScale", DEFAULT_SCALE)
                pCurrentScale = Math.Max(MIN_SCALE, Math.Min(MAX_SCALE, pCurrentScale))
                
                ' Load expanded nodes
                LoadExpandedNodes()
                
                ' Apply scale
                ApplyScale(pCurrentScale)
                
            Catch ex As Exception
                Console.WriteLine($"LoadSettings error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Saves settings to the settings manager
        ''' </summary>
        Private Sub SaveSettings()
            Try
                ' Save unified text scale
                SaveUnifiedTextScale(pCurrentScale)
                
                ' Save expanded nodes
                SaveExpandedNodes()
                
            Catch ex As Exception
                Console.WriteLine($"SaveSettings error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Saves the unified text scale setting
        ''' </summary>
        ''' <param name="vScale">Scale percentage to save</param>
        Private Sub SaveUnifiedTextScale(vScale As Integer)
            Try
                ' Save to unified setting used by both explorers
                pSettingsManager.SetInteger("Explorer.TextScale", vScale)
                pSettingsManager.SaveSettings()
                
                Console.WriteLine($"Saved unified Explorer.TextScale: {vScale}%")
                
            Catch ex As Exception
                Console.WriteLine($"SaveUnifiedTextScale error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Loads expanded nodes from settings
        ''' </summary>
        Private Sub LoadExpandedNodes()
            Try
                Dim lExpandedList As String = pSettingsManager.GetString("ProjectExplorer.ExpandedNodes", "")
                pExpandedNodes.Clear()
                
                If Not String.IsNullOrEmpty(lExpandedList) Then
                    Dim lNodes() As String = lExpandedList.Split("|"c)
                    for each lNode in lNodes
                        If Not String.IsNullOrEmpty(lNode.Trim()) Then
                            pExpandedNodes.Add(lNode.Trim())
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadExpandedNodes error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Saves expanded nodes to settings
        ''' </summary>
        Private Sub SaveExpandedNodes()
            Try
                Dim lExpandedList As String = String.Join("|", pExpandedNodes)
                pSettingsManager.SetString("ProjectExplorer.ExpandedNodes", lExpandedList)
                
            Catch ex As Exception
                Console.WriteLine($"SaveExpandedNodes error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Theme Management =====
        
        ''' <summary>
        ''' Applies the current theme and forces a complete refresh
        ''' </summary>
        Public Sub ApplyTheme()
            Try
                ' Since we're now using the shared ThemeManager,
                ' we just need to redraw with the current theme
                
                ' Force a complete redraw of all visual elements
                If pDrawingArea IsNot Nothing Then
                    pDrawingArea.QueueDraw()
                End If
                
                ' Also refresh the corner box if visible
                If pCornerBox IsNot Nothing AndAlso pCornerBox.Visible Then
                    pCornerBox.QueueDraw()
                End If
                
                ' Update scrollbars to match theme
                If pHScrollBar IsNot Nothing Then
                    pHScrollBar.QueueDraw()
                End If
                
                If pVScrollBar IsNot Nothing Then
                    pVScrollBar.QueueDraw()
                End If
                
                Console.WriteLine($"ProjectExplorer.ApplyTheme: Theme applied and refreshed")
                
            Catch ex As Exception
                Console.WriteLine($"ApplyTheme error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles theme change notifications from the ThemeManager
        ''' </summary>
        Public Sub OnThemeChanged()
            Try
                Console.WriteLine("ProjectExplorer.OnThemeChanged: Received theme change notification")
                
                ' Apply the new theme
                ApplyTheme()
                
            Catch ex As Exception
                Console.WriteLine($"OnThemeChanged error: {ex.Message}")
            End Try
        End Sub

        
        ' ===== Public Properties =====
        
        ''' <summary>
        ''' Gets or sets the current scale percentage
        ''' </summary>
        Public Property Scale As Integer
            Get
                Return pCurrentScale
            End Get
            Set(value As Integer)
                ApplyScale(value)
                SaveUnifiedTextScale(value)
            End Set
        End Property
        
        ''' <summary>
        ''' Gets the current project file path
        ''' </summary>
        Public ReadOnly Property ProjectFile As String
            Get
                Return pProjectFile
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the project directory path
        ''' </summary>
        Public ReadOnly Property ProjectDirectory As String
            Get
                Return pProjectDirectory
            End Get
        End Property
        
        ''' <summary>
        ''' Gets whether a project is loaded
        ''' </summary>
        Public ReadOnly Property HasProject As Boolean
            Get
                Return Not String.IsNullOrEmpty(pProjectFile)
            End Get
        End Property
        
    End Class
    
End Namespace
