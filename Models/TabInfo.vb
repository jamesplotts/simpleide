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
        Public Property LastSaved As DateTime = DateTime.Now  ' ADDED: Track last saved time for git integration
        
        ' Editor components - SIMPLIFIED
        Public Property Editor As IEditor          ' the Editor interface
        Public Property EditorContainer As Widget  ' Container Widget (may Include navigation)
        Public Property TabLabel As Widget          ' Tab label
        Private pDocumentNodes As Dictionary(Of String, DocumentNode)
        Private pRootNodes As List(Of DocumentNode)

        
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
                
                ' Note: We don't dispose GTK widgets as GTK handles that
                
            Catch ex As Exception
                Console.WriteLine($"error disposing TabInfo: {ex.Message}")
            End Try
        End Sub

        ' Document nodes dictionary
        Public Property DocumentNodes As Dictionary(Of String, DocumentNode)
            Get
                If pDocumentNodes Is Nothing Then
                    pDocumentNodes = New Dictionary(Of String, DocumentNode)()
                End If
                Return pDocumentNodes
            End Get
            Set(Value As Dictionary(Of String, DocumentNode))
                pDocumentNodes = Value
            End Set
        End Property
        
        ' Root nodes list
        Public Property RootNodes As List(Of DocumentNode)
            Get
                If pRootNodes Is Nothing Then
                    pRootNodes = New List(Of DocumentNode)()
                End If
                Return pRootNodes
            End Get
            Set(Value As List(Of DocumentNode))
                pRootNodes = Value
            End Set
        End Property
        
        ' Update nodes from editor if it's a CustomDrawingEditor
        Public Sub UpdateNodesFromEditor()
            Try
                If TypeOf Editor Is CustomDrawingEditor Then
                    Dim lCustomEditor As CustomDrawingEditor = DirectCast(Editor, CustomDrawingEditor)
                    DocumentNodes = lCustomEditor.GetDocumentNodes()
                    RootNodes = lCustomEditor.GetRootNodes()
                End If
            Catch ex As Exception
                Console.WriteLine($"UpdateNodesFromEditor error: {ex.Message}")
            End Try
        End Sub

    End Class
End Namespace
