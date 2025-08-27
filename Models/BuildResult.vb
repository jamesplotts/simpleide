' Models/BuildResult.vb - Build result models
Imports System
Imports System.Collections.Generic
Imports System.Text

Namespace Models
    ' Build result container
    Public Class BuildResult
        Public Property Success As Boolean
        Public Property output As String
        Public Property ErrorOutput As String
        Public Property ExitCode As Integer
        Public Property BuildTime As TimeSpan
        Public Property StartTime As DateTime
        Public Property WarningCount As Integer
        Public Property ErrorCount As Integer
        Public Property EndTime As DateTime
        Public Property ProjectPath As String
        Public Property Configuration As String
        Public Property Platform As String
        Public Property Message As String
        Public Property OutputLines As New List(Of BuildOutputLine) ' FIXED: Added missing property
        Public Property Errors As New List(Of BuildError)
        Public Property Warnings As New List(Of BuildWarning)

        
        Public Sub New()
            StartTime = DateTime.Now
            Configuration = "Debug"
            Platform = "any CPU"
        End Sub
        
        Public Sub MarkCompleted()
            EndTime = DateTime.Now
            BuildTime = EndTime - StartTime
        End Sub
        
        Public ReadOnly Property HasErrors As Boolean
            Get
                Return Not Success OrElse ExitCode <> 0
            End Get
        End Property
        
        Public ReadOnly Property FormattedBuildTime As String
            Get
                If BuildTime.TotalSeconds < 1 Then
                    Return $"{BuildTime.TotalMilliseconds:F0}ms"
                Else
                    Return $"{BuildTime.TotalSeconds:F2}s"
                End If
            End Get
        End Property
    End Class
    
    ' FIXED: Added missing BuildOutputLine class
    Public Class BuildOutputLine
        Public Property Text As String
        Public Property Type As BuildOutputType
        Public Property Timestamp As DateTime = DateTime.Now
        
        Public Sub New()
        End Sub
        
        Public Sub New(vText As String, vType As BuildOutputType)
            Text = vText
            Type = vType
        End Sub
    End Class
    
    ' FIXED: Added missing BuildOutputType enum
    Public Enum BuildOutputType
        eUnspecified
        eNormal
        eError
        eWarning
        eInformation
        eSuccess
        eLastValue
    End Enum
    
    ' Build error information
    Public Class BuildError
        Public Property FilePath As String
        Public Property Line As Integer
        Public Property Column As Integer
        Public Property ErrorCode As String
        Public Property Message As String
        Public Property Severity As String
        Public Property project As String
        Public Property code As String
        
        Public Sub New()
            Line = 1
            Column = 1
            Severity = "error"
        End Sub
        
        Public ReadOnly Property DisplayText As String
            Get
                Dim lFileName As String = System.IO.Path.GetFileName(FilePath)
                Return $"{lFileName}({Line},{Column}): {Message}"
            End Get
        End Property
        
        Public ReadOnly Property FullDisplayText As String
            Get
                Dim lCode As String = If(String.IsNullOrEmpty(ErrorCode), "", $" [{ErrorCode}]")
                Return $"{FilePath}({Line},{Column}): error{lCode}: {Message}"
            End Get
        End Property
    End Class
    
    ' Build warning information
    Public Class BuildWarning
        Public Property FilePath As String
        Public Property Line As Integer
        Public Property Column As Integer
        Public Property WarningCode As String
        Public Property Message As String
        Public Property Severity As String
        Public Property project As String
        Public Property code As String        

        Public Sub New()
            Line = 1
            Column = 1
            Severity = "Warning"
        End Sub
        
        Public ReadOnly Property DisplayText As String
            Get
                Dim lFileName As String = System.IO.Path.GetFileName(FilePath)
                Return $"{lFileName}({Line},{Column}): {Message}"
            End Get
        End Property
        
        Public ReadOnly Property FullDisplayText As String
            Get
                Dim lCode As String = If(String.IsNullOrEmpty(WarningCode), "", $" [{WarningCode}]")
                Return $"{FilePath}({Line},{Column}): warning{lCode}: {Message}"
            End Get
        End Property
    End Class
    
    ' Build configuration options
    Public Class BuildConfiguration
        Public Property Configuration As String = "Debug"
        Public Property Platform As String = "any CPU"
        Public Property Verbosity As BuildVerbosity = BuildVerbosity.Minimal
        Public Property OutputPath As String = ""
        Public Property AdditionalArguments As String = ""
        Public Property EnvironmentVariables As New Dictionary(Of String, String)
        Public Property WorkingDirectory As String = ""
        Public Property RestorePackages As Boolean = True
        Public Property CleanFirst As Boolean = False
        Public Property CleanBeforeBuild As Boolean = False
        Public Property ProjectPath As String = ""
        
        Public Sub New()
        End Sub
        
        Public Function GetBuildArguments(vProjectPath As String) As String
            Dim lArgs As New List(Of String)
            
            ' Build command
            lArgs.Add("build")
            
            ' Project path
            lArgs.Add($"""{vProjectPath}""")
            
            ' Configuration
            lArgs.Add($"-c:{Configuration}")
            
            ' Platform (if not default)
            If Not String.IsNullOrEmpty(Platform) AndAlso Platform <> "any CPU" Then
                lArgs.Add($"-p:Platform=""{Platform}""")
            End If
            
            ' Output path
            If Not String.IsNullOrEmpty(OutputPath) Then
                lArgs.Add($"-o:""{OutputPath}""")
            End If
            
            ' Verbosity
            lArgs.Add($"-v:{GetVerbosityString()}")
            
            ' No restore if already done
            If Not RestorePackages Then
                lArgs.Add("--no-restore")
            End If
            
            ' Additional arguments
            If Not String.IsNullOrEmpty(AdditionalArguments) Then
                lArgs.Add(AdditionalArguments)
            End If
            
            Return String.Join(" ", lArgs)
        End Function
        
        Public Function GetDotNetArguments() As String
            Return GetBuildArguments("")  ' Call the existing method for compatibility
        End Function

        ' Get command line arguments for dotnet build (without project path)
        Public Function GetCommandLineArgs() As List(Of String)
            Try
                Dim lArgs As New List(Of String)
                
                ' Build command
                lArgs.Add("build")
                
                ' Configuration
                lArgs.Add($"--Configuration")
                lArgs.Add(Configuration)
                
                ' Platform (if not default)
                If Not String.IsNullOrEmpty(Platform) AndAlso Platform <> "any CPU" Then
                    lArgs.Add($"--property:Platform={Platform}")
                End If
                
                ' Output path
                If Not String.IsNullOrEmpty(OutputPath) Then
                    lArgs.Add($"--output")
                    lArgs.Add(OutputPath)
                End If
                
                ' Verbosity
                lArgs.Add($"--Verbosity")
                lArgs.Add(GetVerbosityString())
                
                ' No restore if already done
                If Not RestorePackages Then
                    lArgs.Add("--no-restore")
                End If
                
                ' Additional arguments (split by spaces, respecting quotes)
                If Not String.IsNullOrEmpty(AdditionalArguments) Then
                    Dim lAdditionalArgs As List(Of String) = ParseAdditionalArguments(AdditionalArguments)
                    lArgs.AddRange(lAdditionalArgs)
                End If
                
                Return lArgs
                
            Catch ex As Exception
                Console.WriteLine($"GetCommandLineArgs error: {ex.Message}")
                Return New List(Of String) From {"build"}
            End Try
        End Function
        
        ' Parse additional arguments respecting quoted strings
        Private Function ParseAdditionalArguments(vArgs As String) As List(Of String)
            Try
                Dim lResult As New List(Of String)
                Dim lInQuotes As Boolean = False
                Dim lCurrentArg As New StringBuilder()
                
                for i As Integer = 0 To vArgs.Length - 1
                    Dim lChar As Char = vArgs(i)
                    
                    If lChar = """"c Then
                        lInQuotes = Not lInQuotes
                    ElseIf lChar = " "c AndAlso Not lInQuotes Then
                        If lCurrentArg.Length > 0 Then
                            lResult.Add(lCurrentArg.ToString().Trim())
                            lCurrentArg.Clear()
                        End If
                    Else
                        lCurrentArg.Append(lChar)
                    End If
                Next
                
                ' Add final argument if any
                If lCurrentArg.Length > 0 Then
                    lResult.Add(lCurrentArg.ToString().Trim())
                End If
                
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"ParseAdditionalArguments error: {ex.Message}")
                Return New List(Of String)
            End Try
        End Function
        
        Private Function GetVerbosityString() As String
            Select Case Verbosity
                Case BuildVerbosity.Quiet
                    Return "q"
                Case BuildVerbosity.Minimal
                    Return "m"
                Case BuildVerbosity.Normal
                    Return "n"
                Case BuildVerbosity.Detailed
                    Return "d"
                Case BuildVerbosity.Diagnostic
                    Return "diag"
                Case Else
                    Return "m"
            End Select
        End Function

        Private pBuildBeforeRun As Boolean = True
        
        ''' <summary>
        ''' Gets or sets whether to build before running the project
        ''' </summary>
        Public Property BuildBeforeRun As Boolean
            Get
                Return pBuildBeforeRun
            End Get
            Set(value As Boolean)
                pBuildBeforeRun = value
            End Set
        End Property

    End Class
    
    ' Build verbosity levels
    Public Enum BuildVerbosity
        Quiet
        Minimal
        Normal
        Detailed
        Diagnostic
    End Enum

End Namespace

