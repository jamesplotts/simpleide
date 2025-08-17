' Models/FindOptions.vb - Find/Replace operation options
Imports System

Namespace Models
    
    ' Find and replace operation options
    Public Class FindOptions
        
        ' Basic search options
        Public Property SearchText As String = ""
        Public Property ReplaceText As String = ""
        Public Property CaseSensitive As Boolean = False
        Public Property WholeWord As Boolean = False
        Public Property UseRegex As Boolean = False
        Public Property SearchBackward As Boolean = False
        
        ' Search scope options
        Public Property SearchPath As String = ""
        Public Property FilePattern As String = "*.*"
        Public Property IncludeSubdirectories As Boolean = True
        Public Property SearchInSelection As Boolean = False
        
        ' File filtering options
        Public Property ExcludePattern As String = ""
        Public Property IncludeBinaryFiles As Boolean = False
        Public Property IncludeHiddenFiles As Boolean = False
        
        ' Replace options
        Public Property ReplaceAll As Boolean = False
        Public Property ConfirmReplacements As Boolean = True
        Public Property CreateBackup As Boolean = False
        
        ' Search context
        Public Property MaxResults As Integer = 1000
        Public Property ShowContext As Boolean = True
        Public Property ContextLines As Integer = 2
        
        Public Sub New()
            ' Default constructor with sensible defaults
        End Sub
        
        Public Sub New(vSearchText As String)
            SearchText = vSearchText
        End Sub
        
        Public Sub New(vSearchText As String, vReplaceText As String)
            SearchText = vSearchText
            ReplaceText = vReplaceText
        End Sub
        
        ' Create options for simple text search
        Public Shared Function CreateSimpleSearch(vSearchText As String, vCaseSensitive As Boolean, vWholeWord As Boolean) As FindOptions
            Return New FindOptions(vSearchText) With {
                .CaseSensitive = vCaseSensitive,
                .WholeWord = vWholeWord
            }
        End Function
        
        ' Create options for regex search
        Public Shared Function CreateRegexSearch(vPattern As String, vCaseSensitive As Boolean) As FindOptions
            Return New FindOptions(vPattern) With {
                .UseRegex = True,
                .CaseSensitive = vCaseSensitive
            }
        End Function
        
        ' Create options for file search
        Public Shared Function CreateFileSearch(vSearchText As String, vSearchPath As String, vFilePattern As String) As FindOptions
            Return New FindOptions(vSearchText) With {
                .SearchPath = vSearchPath,
                .FilePattern = vFilePattern,
                .IncludeSubdirectories = True
            }
        End Function
        
        ' Create options for replace operation
        Public Shared Function CreateReplace(vSearchText As String, vReplaceText As String, vCaseSensitive As Boolean, vWholeWord As Boolean) As FindOptions
            Return New FindOptions(vSearchText, vReplaceText) With {
                .CaseSensitive = vCaseSensitive,
                .WholeWord = vWholeWord,
                .ConfirmReplacements = True
            }
        End Function
        
        ' Clone the options
        Public Function Clone() As FindOptions
            Dim lClone As New FindOptions()
            
            ' Copy all properties
            lClone.SearchText = SearchText
            lClone.ReplaceText = ReplaceText
            lClone.CaseSensitive = CaseSensitive
            lClone.WholeWord = WholeWord
            lClone.UseRegex = UseRegex
            lClone.SearchBackward = SearchBackward
            
            lClone.SearchPath = SearchPath
            lClone.FilePattern = FilePattern
            lClone.IncludeSubdirectories = IncludeSubdirectories
            lClone.SearchInSelection = SearchInSelection
            
            lClone.ExcludePattern = ExcludePattern
            lClone.IncludeBinaryFiles = IncludeBinaryFiles
            lClone.IncludeHiddenFiles = IncludeHiddenFiles
            
            lClone.ReplaceAll = ReplaceAll
            lClone.ConfirmReplacements = ConfirmReplacements
            lClone.CreateBackup = CreateBackup
            
            lClone.MaxResults = MaxResults
            lClone.ShowContext = ShowContext
            lClone.ContextLines = ContextLines
            
            Return lClone
        End Function
        
        ' Get display text for the options
        Public Function GetDisplayText() As String
            Dim lOptions As New List(Of String)()
            
            If CaseSensitive Then lOptions.Add("Match case")
            If WholeWord Then lOptions.Add("Whole word")
            If UseRegex Then lOptions.Add("Regex")
            If SearchBackward Then lOptions.Add("Backward")
            If SearchInSelection Then lOptions.Add("in selection")
            If IncludeSubdirectories Then lOptions.Add("Include subdirs")
            
            If lOptions.Count > 0 Then
                Return String.Join(", ", lOptions)
            Else
                Return "Default options"
            End If
        End Function
        
        ' Validate the options
        Public Function IsValid() As Boolean
            Try
                ' Check required fields
                If String.IsNullOrEmpty(SearchText) Then Return False
                
                ' Validate regex pattern if using regex
                If UseRegex Then
                    Try
                        Dim lTestRegex As New Text.RegularExpressions.Regex(SearchText)
                    Catch
                        Return False
                    End Try
                End If
                
                ' Validate file pattern if searching in files
                If Not String.IsNullOrEmpty(SearchPath) Then
                    If String.IsNullOrEmpty(FilePattern) Then Return False
                End If
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"FindOptions.IsValid error: {ex.Message}")
                Return False
            End Try
        End Function
        
    End Class
    
End Namespace
