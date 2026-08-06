' Models/Solution.vb - Parsed representation of a Visual Studio .sln file
Imports System
Imports System.Collections.Generic

Namespace Models

    ''' <summary>
    ''' Represents a single project entry parsed from a .sln file
    ''' </summary>
    Public Class SolutionProjectEntry

        ''' <summary>
        ''' Gets or sets the project name as it appears in the .sln file
        ''' </summary>
        Public Property Name As String

        ''' <summary>
        ''' Gets or sets the full, resolved path to the project file (.vbproj/.csproj/.fsproj)
        ''' </summary>
        Public Property ProjectPath As String

        ''' <summary>
        ''' Gets or sets the project GUID as it appears in the .sln file
        ''' </summary>
        Public Property ProjectGuid As String

    End Class

    ''' <summary>
    ''' Represents a parsed .sln solution file - the set of member projects it references
    ''' </summary>
    ''' <remarks>
    ''' Deliberately minimal for now: just enough to drive SolutionManager.LoadSolution.
    ''' Solution-level build configuration mapping (the GlobalSection blocks) is parsed by
    ''' SolutionFileParser for validation but not retained here, since nothing yet consumes it.
    ''' </remarks>
    Public Class Solution

        ''' <summary>
        ''' Gets or sets the full path to the .sln file itself
        ''' </summary>
        Public Property SolutionPath As String

        ''' <summary>
        ''' Gets or sets the directory containing the .sln file - project paths in the file are
        ''' relative to this
        ''' </summary>
        Public Property SolutionDirectory As String

        ''' <summary>
        ''' Gets the member projects declared in this solution, in the order they appear in the
        ''' .sln file
        ''' </summary>
        Public Property Projects As New List(Of SolutionProjectEntry)()

    End Class

End Namespace
