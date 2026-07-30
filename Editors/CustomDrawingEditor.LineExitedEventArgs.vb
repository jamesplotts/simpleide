' Editors/CustomDrawingEditor.LineExitedEventArgs.vb
Imports System

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        
        ' ===== LineExited Event and EventArgs =====
        
        ''' <summary>
        ''' Event arguments for when cursor exits a line
        ''' </summary>
        Public Class LineExitedEventArgs
            Inherits EventArgs
            
            Private pLineNumber As Integer
            Private pLineText As String
            
            ''' <summary>
            ''' Gets the line number that was exited (0-based)
            ''' </summary>
            Public ReadOnly Property LineNumber As Integer
                Get
                    Return pLineNumber
                End Get
            End Property
            
            ''' <summary>
            ''' Gets the text content of the line that was exited
            ''' </summary>
            Public ReadOnly Property LineText As String
                Get
                    Return pLineText
                End Get
            End Property
            
            ''' <summary>
            ''' Creates new LineExitedEventArgs
            ''' </summary>
            Public Sub New(vLineNumber As Integer, vLineText As String)
                pLineNumber = vLineNumber
                pLineText = vLineText
            End Sub
        End Class
        
        ' ===== IdentifierScope Enum =====
        ' This enum is referenced by IdentifierCapitalizationManager
        Public Enum IdentifierScope
            eUnspecified
            eLocal
            eVariable
            eField
            eProperty
            eMethod
            eFunction
            eType
            eEvent
            eConstant
            eParameter
            eLastValue
        End Enum
        
    End Class
    
End Namespace