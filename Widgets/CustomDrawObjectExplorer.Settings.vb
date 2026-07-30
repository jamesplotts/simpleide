' Widgets/CustomDrawObjectExplorer.Settings.vb - Settings management for Object Explorer
' Created: 2025-08-16
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Syntax
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing settings management for the Object Explorer
    ''' </summary>
    Partial Public Class CustomDrawObjectExplorer
        Inherits Box
        Implements IObjectExplorer
        
        ' ===== Settings Loading =====
        
        ''' <summary>
        ''' Loads settings from the settings manager
        ''' </summary>
        Private Sub LoadSettings()
            Try
                ' Load unified scale setting (shared with Project Explorer)
                pCurrentScale = pSettingsManager.GetInteger("Explorer.TextScale", DEFAULT_SCALE)
                pCurrentScale = Math.Max(MIN_SCALE, Math.Min(MAX_SCALE, pCurrentScale))
                
                ' Load sort mode
                Dim lSortMode As String = pSettingsManager.GetString("ObjectExplorer.SortMode", "Default")
                Select Case lSortMode.ToLower()
                    Case "alphabetic"
                        pSortMode = ObjectExplorerSortMode.eAlphabetic
                    Case "bytype"
                        pSortMode = ObjectExplorerSortMode.eByType
                    Case "byvisibility"
                        pSortMode = ObjectExplorerSortMode.eByVisibility
                    Case Else
                        pSortMode = ObjectExplorerSortMode.eDefault
                End Select
                
                ' Load display settings
                pShowPrivateMembers = pSettingsManager.GetBoolean("ObjectExplorer.ShowPrivateMembers", True)
                pShowInheritedMembers = pSettingsManager.GetBoolean("ObjectExplorer.ShowInheritedMembers", False)
                pShowRegions = pSettingsManager.GetBoolean("ObjectExplorer.ShowRegions", False)
                
                ' Load expanded nodes
                LoadExpandedNodes()
                
                Console.WriteLine($"Settings loaded with unified scale: {pCurrentScale}%")
                
            Catch ex As Exception
                Console.WriteLine($"LoadSettings error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Loads expanded nodes from settings
        ''' </summary>
        Private Sub LoadExpandedNodes()
            Try
                pExpandedNodes.Clear()
                
                Dim lExpandedList As String = pSettingsManager.GetString("ObjectExplorer.ExpandedNodes", "")
                If Not String.IsNullOrEmpty(lExpandedList) Then
                    Dim lNodes As String() = lExpandedList.Split("|"c)
                    For Each lNode In lNodes
                        If Not String.IsNullOrWhiteSpace(lNode) Then
                            pExpandedNodes.Add(lNode.Trim())
                        End If
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadExpandedNodes error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Loads member order preferences for sorting
        ''' </summary>
        Private Sub LoadMemberOrderPreferences()
            Try
                ' Load custom member order if user has preferences
                ' Example: "Constructor,Property,Method,Function,Event,Field"
                Dim lMemberOrder As String = pSettingsManager.GetString("ObjectExplorer.MemberOrder", "")
                
                If Not String.IsNullOrEmpty(lMemberOrder) Then
                    ' Parse and store member order
                    ' TODO: Implement custom member ordering
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadMemberOrderPreferences error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Settings Saving =====
        
        ''' <summary>
        ''' Saves current settings to the settings manager
        ''' </summary>
        Public Sub SaveSettings()
            Try
                ' Save scale
                SaveScaleSetting()
                
                ' Save sort mode
                SaveSortModeSetting()
                
                ' Save display settings
                pSettingsManager.SetBoolean("ObjectExplorer.ShowPrivateMembers", pShowPrivateMembers)
                pSettingsManager.SetBoolean("ObjectExplorer.ShowInheritedMembers", pShowInheritedMembers)
                pSettingsManager.SetBoolean("ObjectExplorer.ShowRegions", pShowRegions)
                
                ' Save expanded nodes
                SaveExpandedNodes()
                
                ' Persist to disk
                pSettingsManager.SaveSettings()
                
            Catch ex As Exception
                Console.WriteLine($"SaveSettings error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Saves the current scale setting
        ''' </summary>
        Private Sub SaveScaleSetting()
            Try
                pSettingsManager.SetInteger("ObjectExplorer.Scale", pCurrentScale)
                
            Catch ex As Exception
                Console.WriteLine($"SaveScaleSetting error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Saves the current sort mode setting
        ''' </summary>
        Private Sub SaveSortModeSetting()
            Try
                Dim lSortMode As String = "Default"
                Select Case pSortMode
                    Case ObjectExplorerSortMode.eAlphabetic
                        lSortMode = "Alphabetic"
                    Case ObjectExplorerSortMode.eByType
                        lSortMode = "ByType"
                    Case ObjectExplorerSortMode.eByVisibility
                        lSortMode = "ByVisibility"
                End Select
                
                pSettingsManager.SetString("ObjectExplorer.SortMode", lSortMode)
                
            Catch ex As Exception
                Console.WriteLine($"SaveSortModeSetting error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Saves expanded nodes to settings
        ''' </summary>
        Private Sub SaveExpandedNodes()
            Try
                Dim lExpandedList As String = String.Join("|", pExpandedNodes)
                pSettingsManager.SetString("ObjectExplorer.ExpandedNodes", lExpandedList)
                
            Catch ex As Exception
                Console.WriteLine($"SaveExpandedNodes error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Setting Properties =====
        
        ''' <summary>
        ''' Gets or sets the current scale percentage
        ''' </summary>
        Public Property Scale As Integer
            Get
                Return pCurrentScale
            End Get
            Set(value As Integer)
                ApplyScale(value)
                SaveScaleSetting()
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets the sort mode
        ''' </summary>
        Public Property SortMode As ObjectExplorerSortMode
            Get
                Return pSortMode
            End Get
            Set(value As ObjectExplorerSortMode)
                If pSortMode <> value Then
                    pSortMode = value
                    SaveSortModeSetting()
                    RebuildVisualTree()
                    pDrawingArea?.QueueDraw()
                End If
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether private members are shown
        ''' </summary>
        Public Property ShowPrivateMembers As Boolean
            Get
                Return pShowPrivateMembers
            End Get
            Set(value As Boolean)
                If pShowPrivateMembers <> value Then
                    pShowPrivateMembers = value
                    pSettingsManager.SetBoolean("ObjectExplorer.ShowPrivateMembers", value)
                    RebuildVisualTree()
                    pDrawingArea?.QueueDraw()
                End If
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether inherited members are shown
        ''' </summary>
        Public Property ShowInheritedMembers As Boolean
            Get
                Return pShowInheritedMembers
            End Get
            Set(value As Boolean)
                If pShowInheritedMembers <> value Then
                    pShowInheritedMembers = value
                    pSettingsManager.SetBoolean("ObjectExplorer.ShowInheritedMembers", value)
                    RebuildVisualTree()
                    pDrawingArea?.QueueDraw()
                End If
            End Set
        End Property
        
        ''' <summary>
        ''' Gets or sets whether regions are shown
        ''' </summary>
        Public Property ShowRegions As Boolean
            Get
                Return pShowRegions
            End Get
            Set(value As Boolean)
                If pShowRegions <> value Then
                    pShowRegions = value
                    pSettingsManager.SetBoolean("ObjectExplorer.ShowRegions", value)
                    RebuildVisualTree()
                    pDrawingArea?.QueueDraw()
                End If
            End Set
        End Property
        
        ' ===== Preference Methods =====
        
        ''' <summary>
        ''' Sets the member display order for sorting
        ''' </summary>
        Public Sub SetMemberOrder(vOrder As List(Of CodeNodeType))
            Try
                ' Convert to string for storage
                Dim lOrderStrings As New List(Of String)
                For Each lType In vOrder
                    lOrderStrings.Add(lType.ToString())
                Next
                
                Dim lOrderString As String = String.Join(",", lOrderStrings)
                pSettingsManager.SetString("ObjectExplorer.MemberOrder", lOrderString)
                
                ' Apply if using type sorting
                If pSortMode = ObjectExplorerSortMode.eByType Then
                    RebuildVisualTree()
                    pDrawingArea?.QueueDraw()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"SetMemberOrder error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Resets all settings to defaults
        ''' </summary>
        Public Sub ResetToDefaults()
            Try
                ' Reset scale
                pCurrentScale = DEFAULT_SCALE
                ApplyScale(pCurrentScale)
                
                ' Reset sort mode
                pSortMode = ObjectExplorerSortMode.eDefault
                
                ' Reset display settings
                pShowPrivateMembers = True
                pShowInheritedMembers = False
                pShowRegions = False
                
                ' Clear expanded nodes
                pExpandedNodes.Clear()
                
                ' Save all settings
                SaveSettings()
                
                ' Rebuild display
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ResetToDefaults error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Exports current settings to a dictionary
        ''' </summary>
        Public Function ExportSettings() As Dictionary(Of String, Object)
            Try
                Dim lSettings As New Dictionary(Of String, Object)
                
                lSettings("Scale") = pCurrentScale
                lSettings("SortMode") = pSortMode.ToString()
                lSettings("ShowPrivateMembers") = pShowPrivateMembers
                lSettings("ShowInheritedMembers") = pShowInheritedMembers
                lSettings("ShowRegions") = pShowRegions
                lSettings("ExpandedNodes") = New List(Of String)(pExpandedNodes)
                
                Return lSettings
                
            Catch ex As Exception
                Console.WriteLine($"ExportSettings error: {ex.Message}")
                Return New Dictionary(Of String, Object)
            End Try
        End Function
        
        ''' <summary>
        ''' Imports settings from a dictionary
        ''' </summary>
        Public Sub ImportSettings(vSettings As Dictionary(Of String, Object))
            Try
                If vSettings.ContainsKey("Scale") Then
                    pCurrentScale = Convert.ToInt32(vSettings("Scale"))
                    ApplyScale(pCurrentScale)
                End If
                
                If vSettings.ContainsKey("SortMode") Then
                    Dim lSortModeStr As String = vSettings("SortMode").ToString()
                    [Enum].TryParse(Of ObjectExplorerSortMode)(lSortModeStr, pSortMode)
                End If
                
                If vSettings.ContainsKey("ShowPrivateMembers") Then
                    pShowPrivateMembers = Convert.ToBoolean(vSettings("ShowPrivateMembers"))
                End If
                
                If vSettings.ContainsKey("ShowInheritedMembers") Then
                    pShowInheritedMembers = Convert.ToBoolean(vSettings("ShowInheritedMembers"))
                End If
                
                If vSettings.ContainsKey("ShowRegions") Then
                    pShowRegions = Convert.ToBoolean(vSettings("ShowRegions"))
                End If
                
                If vSettings.ContainsKey("ExpandedNodes") Then
                    pExpandedNodes.Clear()
                    Dim lNodes As List(Of String) = TryCast(vSettings("ExpandedNodes"), List(Of String))
                    If lNodes IsNot Nothing Then
                        For Each lNode In lNodes
                            pExpandedNodes.Add(lNode)
                        Next
                    End If
                End If
                
                ' Save and rebuild
                SaveSettings()
                RebuildVisualTree()
                pDrawingArea?.QueueDraw()
                
            Catch ex As Exception
                Console.WriteLine($"ImportSettings error: {ex.Message}")
            End Try
        End Sub

        
    End Class
    
End Namespace
