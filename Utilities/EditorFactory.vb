' Refactored EditorFactory.vb - Work with SourceFileInfo and ProjectManager

Imports System
Imports System.IO
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Editors
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Namespace Utilities
    
    Public Class EditorFactory
        
        Private Shared pSyntaxColorSet As SyntaxColorSet
        Private Shared pSettingsManager As SettingsManager
        Private Shared pProjectManager As ProjectManager
        
        ' Initialize the factory with required dependencies
        Public Shared Sub Initialize(vSyntaxColorSet As SyntaxColorSet, 
                                    vSettingsManager As SettingsManager,
                                    vProjectManager As ProjectManager)
            pSyntaxColorSet = vSyntaxColorSet
            pSettingsManager = vSettingsManager
            pProjectManager = vProjectManager
            Console.WriteLine("EditorFactory initialized with ProjectManager support")
        End Sub
        
        ' Create an editor for a SourceFileInfo
        Public Shared Function CreateEditor(vSourceFileInfo As SourceFileInfo) As IEditor
            Try
                ' Validate dependencies
                If pSyntaxColorSet Is Nothing OrElse pSettingsManager Is Nothing Then
                    Throw New InvalidOperationException("EditorFactory not initialized. Call Initialize() first.")
                End If
                
                If vSourceFileInfo Is Nothing Then
                    Throw New ArgumentNullException(NameOf(vSourceFileInfo), "SourceFileInfo cannot be null")
                End If
                
                Console.WriteLine($"EditorFactory.CreateEditor: Creating CustomDrawingEditor for {vSourceFileInfo.FileName}")
                
                ' Load content if not already loaded
                If Not vSourceFileInfo.IsLoaded Then
                    If Not vSourceFileInfo.LoadContent() Then
                        Console.WriteLine($"Warning: Could not load content for {vSourceFileInfo.FileName}")
                        ' Continue with empty content
                    End If
                End If
                
                ' Create CustomDrawingEditor with SourceFileInfo
                Dim lEditor As New CustomDrawingEditor(vSourceFileInfo)
                
                ' Set dependencies
                lEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                
                ' Link the editor back to SourceFileInfo
                vSourceFileInfo.Editor = lEditor
                
                Console.WriteLine($"Successfully created editor for {vSourceFileInfo.FileName}")
                Return lEditor
                
            Catch ex As Exception
                Console.WriteLine($"EditorFactory.CreateEditor error: {ex.Message}")
                Throw
            End Try
        End Function
        
        ' Create an editor for a file path (legacy support)
        Public Shared Function CreateEditor(vFilePath As String) As IEditor
            Try
                ' Get or create SourceFileInfo through ProjectManager
                Dim lSourceFileInfo As SourceFileInfo = Nothing
                
                If pProjectManager IsNot Nothing Then
                    ' Try to get existing SourceFileInfo from ProjectManager
                    lSourceFileInfo = pProjectManager.GetSourceFileInfo(vFilePath)
                End If
                
                ' If not found, create new SourceFileInfo
                If lSourceFileInfo Is Nothing Then
                    Dim lProjectDir As String = ""
                    If pProjectManager IsNot Nothing AndAlso pProjectManager.CurrentProjectInfo IsNot Nothing Then
                        lProjectDir = Path.GetDirectoryName(pProjectManager.CurrentProjectInfo.ProjectPath)
                    Else
                        lProjectDir = Path.GetDirectoryName(vFilePath)
                    End If
                    
                    lSourceFileInfo = New SourceFileInfo(vFilePath, lProjectDir)
                    
                    ' Register with ProjectManager if available
                    If pProjectManager IsNot Nothing Then
                        pProjectManager.RegisterSourceFileInfo(vFilePath, lSourceFileInfo)
                    End If
                End If
                
                ' Create editor with SourceFileInfo
                Return CreateEditor(lSourceFileInfo)
                
            Catch ex As Exception
                Console.WriteLine($"EditorFactory.CreateEditor(String) error: {ex.Message}")
                Throw
            End Try
        End Function
        
        ' Create a new empty editor
        Public Shared Function CreateNewEditor(vFilePath As String) As IEditor
            Try
                ' Create new SourceFileInfo for the new file
                Dim lProjectDir As String = ""
                If pProjectManager IsNot Nothing AndAlso pProjectManager.CurrentProjectInfo IsNot Nothing Then
                    lProjectDir = Path.GetDirectoryName(pProjectManager.CurrentProjectInfo.ProjectPath)
                Else
                    lProjectDir = Path.GetDirectoryName(vFilePath)
                End If
                
                Dim lSourceFileInfo As New SourceFileInfo(vFilePath, lProjectDir)
                
                ' Initialize with empty content
                lSourceFileInfo.Content = ""
                lSourceFileInfo.TextLines = New List(Of String) From {""}
                lSourceFileInfo.IsLoaded = True
                
                ' Register with ProjectManager if available
                If pProjectManager IsNot Nothing Then
                    pProjectManager.RegisterSourceFileInfo(vFilePath, lSourceFileInfo)
                End If
                
                ' Create editor
                Return CreateEditor(lSourceFileInfo)
                
            Catch ex As Exception
                Console.WriteLine($"EditorFactory.CreateNewEditor error: {ex.Message}")
                Throw
            End Try
        End Function
        
        ' Check if a file type is supported
        Public Shared Function IsFileTypeSupported(vFilePath As String) As Boolean
            Dim lExtension As String = Path.GetExtension(vFilePath).ToLower()
            
            ' All text files are supported
            Select Case lExtension
                Case ".vb", ".vbproj", ".txt", ".xml", ".json", ".md", ".cs", ".js", ".html", ".css", ".config", ".resx"
                    Return True
                Case Else
                    ' Also support files with no extension (like README, Makefile, etc.)
                    Return String.IsNullOrEmpty(lExtension)
            End Select
        End Function
        
        ' Get list of supported file extensions
        Public Shared Function GetSupportedExtensions() As String()
            Return {".vb", ".vbproj", ".txt", ".xml", ".json", ".md", ".cs", ".js", ".html", ".css", ".config", ".resx"}
        End Function
        
    End Class
    
End Namespace
