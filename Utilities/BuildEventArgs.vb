' Utilities/BuildEventArgs.vb - Build event argument classes
Imports System

Namespace Models
    ' Build event arguments
    Public Class BuildEventArgs
        Inherits EventArgs
        
        Public Property Result As BuildResult
        Public Property ProjectPath As String
        Public Property StartTime As DateTime
        Public Property EndTime As DateTime
        
        Public Sub New()
            Result = New BuildResult()
            StartTime = DateTime.Now
        End Sub
        
        Public Sub New(vResult As BuildResult)
            Result = vResult
            StartTime = DateTime.Now
        End Sub

        Private pOutput As String
        
        Public Sub New(vOutput As String)
            pOutput = vOutput
        End Sub
        
        Public ReadOnly Property Output As String
            Get
                Return pOutput
            End Get
        End Property

    End Class
    
    ' Build output event arguments
    Public Class BuildOutputEventArgs
        Inherits EventArgs
        
        Public Property Text As String
        Public Property IsError As Boolean
        
        Public Sub New(vText As String, vIsError As Boolean)
            Text = vText
            IsError = vIsError
        End Sub
    End Class
    
    ' Build progress event arguments
    Public Class BuildProgressEventArgs
        Inherits EventArgs
        
        Public Property Progress As Double
        Public Property Message As String
        
        Public Sub New(vProgress As Double, vMessage As String)
            Progress = vProgress
            Message = vMessage
        End Sub
    End Class


End Namespace
