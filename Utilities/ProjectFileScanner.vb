' Utilities/ProjectFileScanner.vb - Shared VB.NET source file enumeration that skips
' version-control/build/tooling directories
Imports System
Imports System.Collections.Generic
Imports System.IO

Namespace Utilities

    ''' <summary>
    ''' Recursively enumerates *.vb files under a project root while skipping directories
    ''' that should never contribute source files to a scan
    ''' </summary>
    Public Class ProjectFileScanner

        ''' <summary>Directory names never recursed into - VCS internals, build output, IDE
        ''' caches, and (specific to this project) nested git worktree checkouts under
        ''' .claude, any of which would otherwise contribute duplicate or stale files to a
        ''' scan rooted at the project directory</summary>
        Private Shared ReadOnly ExcludedDirectoryNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".git", ".claude", ".vs", "bin", "obj", "node_modules", "packages"
        }

        ''' <summary>
        ''' Recursively finds all *.vb files under vRootPath, skipping VCS/build/tooling
        ''' directories so nested worktrees or build output never contribute duplicate or
        ''' stale files
        ''' </summary>
        ''' <param name="vRootPath">Directory to scan</param>
        ''' <returns>Full paths of every *.vb file found</returns>
        Public Shared Function GetVBFiles(vRootPath As String) As List(Of String)
            Dim lResults As New List(Of String)
            Try
                If String.IsNullOrEmpty(vRootPath) OrElse Not Directory.Exists(vRootPath) Then
                    Return lResults
                End If
                ScanDirectory(vRootPath, lResults)
            Catch ex As Exception
                Console.WriteLine($"ProjectFileScanner.GetVBFiles error: {ex.Message}")
            End Try
            Return lResults
        End Function

        Private Shared Sub ScanDirectory(vDirectoryPath As String, vResults As List(Of String))
            Try
                vResults.AddRange(Directory.GetFiles(vDirectoryPath, "*.vb"))

                For Each lSubDirectory In Directory.GetDirectories(vDirectoryPath)
                    Dim lName As String = Path.GetFileName(lSubDirectory)
                    If Not ExcludedDirectoryNames.Contains(lName) Then
                        ScanDirectory(lSubDirectory, vResults)
                    End If
                Next
            Catch ex As Exception
                Console.WriteLine($"ProjectFileScanner.ScanDirectory error for '{vDirectoryPath}': {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
