' Managers/ProjectInfo.vb - Extended ProjectInfo with RootNamespace
Imports System.Collections.Generic
Imports SimpleIDE.Utilities

' ProjectInfo.vb
' Created: 2025-08-10 13:17:12

Namespace Managers
    
    ''' <summary>
    ''' Extended project information that includes root namespace
    ''' </summary>
    Partial Public Class ProjectInfo
        Inherits ProjectFileParser.ProjectInfo
        
        
        Public Property IdentifierCaseMap As Dictionary(Of String, String)
        'Public Property SourceFiles As List(Of String)

        ' ===== Additional Properties =====
        
        ''' <summary>
        ''' List of source files (full paths)
        ''' </summary>
        Public Property SourceFiles As New List(Of String)()
        
        ''' <summary>
        ''' Project metadata like output type, target framework, etc.
        ''' </summary>
        Public Property Metadata As New Dictionary(Of String, String)()
        
        ''' <summary>
        ''' Indicates if the project has unsaved changes
        ''' </summary>
        Public Property IsDirty As Boolean = False
        
        ''' <summary>
        ''' Last build configuration (Debug/Release)
        ''' </summary>
        Public Property LastBuildConfiguration As String = "Debug"
        
        ''' <summary>
        ''' Last build result
        ''' </summary>
        Public Property LastBuildSucceeded As Boolean = False
        
        ' ===== Constructor =====
        
        Public Sub New()
            MyBase.New()
        End Sub
        
        ''' <summary>
        ''' Create from parsed project info
        ''' </summary>
        Public Sub New(vParsedInfo As ProjectFileParser.ProjectInfo)
            If vParsedInfo IsNot Nothing Then
                Me.ProjectName = vParsedInfo.ProjectName
                Me.ProjectPath = vParsedInfo.ProjectPath
                Me.ProjectDirectory = vParsedInfo.ProjectDirectory
                Me.RootNamespace = vParsedInfo.RootNamespace
                Me.CompileItems = vParsedInfo.CompileItems
                Me.References = vParsedInfo.References
                Me.PackageReferences = vParsedInfo.PackageReferences
                
                ' Initialize source files from compile items
                For Each lItem In Me.CompileItems
                    Me.SourceFiles.Add(System.IO.Path.Combine(Me.ProjectDirectory, lItem))
                Next
            End If
            IdentifierCaseMap = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            SourceFiles = New List(Of String)()
        End Sub
        
        ' ===== Methods =====
        
        ''' <summary>
        ''' Get the effective root namespace (never empty)
        ''' </summary>
        Public Function GetEffectiveRootNamespace() As String
            If Not String.IsNullOrEmpty(RootNamespace) Then
                Return RootNamespace
            End If
            Return ProjectName
        End Function
        
        ''' <summary>
        ''' Check if a file is part of this project
        ''' </summary>
        Public Function ContainsFile(vFilePath As String) As Boolean
            Dim lFullPath As String = System.IO.Path.GetFullPath(vFilePath)
            Return SourceFiles.Any(Function(f) System.IO.Path.GetFullPath(f).Equals(lFullPath, StringComparison.OrdinalIgnoreCase))
        End Function
        
        ''' <summary>
        ''' Get relative path for a file in the project
        ''' </summary>
        Public Function GetRelativePath(vFilePath As String) As String
            Try
                Dim lFullPath As String = System.IO.Path.GetFullPath(vFilePath)
                Dim lProjectDir As String = System.IO.Path.GetFullPath(ProjectDirectory)
                
                If lFullPath.StartsWith(lProjectDir, StringComparison.OrdinalIgnoreCase) Then
                    Dim lRelative As String = lFullPath.Substring(lProjectDir.Length)
                    If lRelative.StartsWith(System.IO.Path.DirectorySeparatorChar) Then
                        lRelative = lRelative.Substring(1)
                    End If
                    Return lRelative
                End If
                
                Return vFilePath
                
            Catch ex As Exception
                Console.WriteLine($"GetRelativePath error: {ex.Message}")
                Return vFilePath
            End Try
        End Function
        
    End Class
    
End Namespace
