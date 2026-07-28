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

End Namespace
