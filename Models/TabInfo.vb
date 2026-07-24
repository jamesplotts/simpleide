' Models/TabInfo.vb - Simplified tab information model using IEditor interface
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Widgets
Imports SimpleIDE.Editors

Namespace Models
    Public Class TabInfo
        Implements IDisposable
        
        ' File information
        Public Property FilePath As String
        Public Property Modified As Boolean = False
        Public Property IsProjectFile As Boolean = False
        Public Property IsPngFile As Boolean = False
        Public Property IsThemeEditor As Boolean = False
        Public Property IsSpecialTab as Boolean = False
        Public Property LastSaved As DateTime = DateTime.Now  ' ADDED: Track last saved time for git integration
        
        ' Editor components - SIMPLIFIED
        Public Property Editor As IEditor          ' the Editor interface
        Public Property EditorContainer As Widget  ' Container Widget (may Include navigation)
        Public Property TabLabel As Widget          ' Tab label


        ' Navigation support (optional)
        Public Property NavigationDropdowns As NavigationDropdowns
        
        ' Dispose resources
        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                ' Dispose editor if it implements IDisposable
                Dim lDisposableEditor As IDisposable = TryCast(Editor, IDisposable)
                If lDisposableEditor IsNot Nothing Then
                    lDisposableEditor.Dispose()
                End If
                
                ' Clean up navigation dropdowns
                If NavigationDropdowns IsNot Nothing Then
                    ' NavigationDropdowns cleanup handled by parent container
                    NavigationDropdowns = Nothing
                End If
                
                ' CRITICAL FIX: Properly destroy the GTK container widget
                ' This is essential to prevent the widget from lingering in GTK's internal tracking
                If EditorContainer IsNot Nothing Then
                    ' If it's a container, recursively destroy all children
                    If TypeOf EditorContainer Is Container Then
                        Dim lContainer As Container = CType(EditorContainer, Container)
                        ' Remove all children from the container first
                        For Each lChild In lContainer.Children
                            lContainer.Remove(lChild)
                            ' Destroy child widgets if they're not already disposed
                            If lChild IsNot Nothing Then
                                lChild.Destroy()
                            End If
                        Next
                    End If
                    
                    ' Now destroy the container itself
                    EditorContainer.Destroy()
                    EditorContainer = Nothing
                End If
                
                ' Also destroy the tab label widget
                If TabLabel IsNot Nothing Then
                    TabLabel.Destroy()
                    TabLabel = Nothing
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error disposing TabInfo: {ex.Message}")
            End Try
        End Sub

    End Class
End Namespace
