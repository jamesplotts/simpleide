' Utilities/SolutionFileParser.vb - Parser for Visual Studio .sln solution files
Imports System
Imports System.IO
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models

Namespace Utilities

    ''' <summary>
    ''' Parses the plain-text Visual Studio .sln solution file format - line-oriented, not XML
    ''' </summary>
    ''' <remarks>
    ''' A .sln file's project entries look like:
    ''' Project("{typeGuid}") = "Name", "relative\path.vbproj", "{projectGuid}"
    ''' EndProject
    ''' Solution folders and non-.NET project types use the same Project(...) syntax with a
    ''' different type GUID and a path that isn't a real project file - rather than maintaining
    ''' a list of type GUIDs to recognize, entries are filtered by whether their path has a
    ''' known project file extension (ProjectFileParser.IsProjectFile), which is simpler and
    ''' automatically stays correct if new solution-folder-like entry types appear.
    ''' </remarks>
    Public Class SolutionFileParser

        Private Shared ReadOnly pProjectLineRegex As New Regex(
            "^Project\(""(?<typeguid>\{[^}]+\})""\)\s*=\s*""(?<name>[^""]+)""\s*,\s*""(?<path>[^""]+)""\s*,\s*""(?<guid>\{[^}]+\})""\s*$",
            RegexOptions.Compiled)

        ''' <summary>
        ''' Parses a .sln file into a Solution, resolving each member project's path to a full,
        ''' absolute path relative to the .sln file's own directory
        ''' </summary>
        ''' <param name="vSolutionPath">Full path to the .sln file</param>
        ''' <returns>The parsed Solution, or Nothing if the file couldn't be read/parsed</returns>
        Public Shared Function ParseSolutionFile(vSolutionPath As String) As Solution
            Try
                If Not File.Exists(vSolutionPath) Then
                    #If DEBUG Then
                    Console.WriteLine($"SolutionFileParser: Solution file not found: {vSolutionPath}")
                    #End If
                    Return Nothing
                End If

                Dim lSolution As New Solution()
                lSolution.SolutionPath = vSolutionPath
                lSolution.SolutionDirectory = Path.GetDirectoryName(vSolutionPath)

                for each lLine As String in File.ReadAllLines(vSolutionPath)
                    Dim lMatch As Match = pProjectLineRegex.Match(lLine.Trim())
                    If Not lMatch.Success Then Continue for

                    Dim lRelativePath As String = lMatch.Groups("path").Value.Replace("\"c, Path.DirectorySeparatorChar)
                    If Not ProjectFileParser.IsProjectFile(lRelativePath) Then Continue for

                    Dim lEntry As New SolutionProjectEntry()
                    lEntry.Name = lMatch.Groups("name").Value
                    lEntry.ProjectGuid = lMatch.Groups("guid").Value
                    lEntry.ProjectPath = Path.GetFullPath(Path.Combine(lSolution.SolutionDirectory, lRelativePath))
                    lSolution.Projects.Add(lEntry)
                Next

                #If DEBUG Then
                Console.WriteLine($"SolutionFileParser: Parsed {lSolution.Projects.Count} project(s) from {vSolutionPath}")
                #End If
                Return lSolution

            Catch ex As Exception
                Console.WriteLine($"SolutionFileParser.ParseSolutionFile error: {ex.Message}")
                Return Nothing
            End Try
        End Function

    End Class

End Namespace
