' EditorPosition.vb
' Created: 2025-08-22 10:44:21

Imports System

Namespace Models
    
    ''' <summary>
    ''' Position in the editor with 0-based line and column coordinates
    ''' </summary>
    Public Structure EditorPosition
        ''' <summary>0-based line number</summary>
        Public Line As Integer
        ''' <summary>0-based column position</summary>
        Public Column As Integer
        
        ''' <summary>
        ''' Creates a new EditorPosition with specified line and column
        ''' </summary>
        ''' <param name="vLine">0-based line number</param>
        ''' <param name="vColumn">0-based column position</param>
        Public Sub New(vLine As Integer, vColumn As Integer)
            Line = vLine
            Column = vColumn
        End Sub
        
        ''' <summary>
        ''' Returns a string representation of the position
        ''' </summary>
        Public Overrides Function ToString() As String
            Return $"Line: {Line}, Column: {Column}"
        End Function
        
        ''' <summary>
        ''' Checks equality with another EditorPosition
        ''' </summary>
        Public Overrides Function Equals(vObj As Object) As Boolean
            If TypeOf vObj Is EditorPosition Then
                Dim lOther As EditorPosition = DirectCast(vObj, EditorPosition)
                Return Line = lOther.Line AndAlso Column = lOther.Column
            End If
            Return False
        End Function
        
        Public Function IsLessThan(vObj as Object) as Boolean
            If TypeOf vObj Is EditorPosition Then
                Dim lOther As EditorPosition = DirectCast(vObj, EditorPosition)
                if Line < lOther.Line then return true
                if line = lOther.Line AndAlso Column < lOther.Column then return true
            End If
            Return False
        End Function
        
        
        ''' <summary>
        ''' Gets hash code for the position
        ''' </summary>
        Public Overrides Function GetHashCode() As Integer
            Return Line.GetHashCode() Xor Column.GetHashCode()
        End Function
        
        ''' <summary>
        ''' Equality operator
        ''' </summary>
        Public Shared Operator =(vLeft As EditorPosition, vRight As EditorPosition) As Boolean
            Return vLeft.Line = vRight.Line AndAlso vLeft.Column = vRight.Column
        End Operator
        
        ''' <summary>
        ''' Inequality operator
        ''' </summary>
        Public Shared Operator <>(vLeft As EditorPosition, vRight As EditorPosition) As Boolean
            Return Not (vLeft = vRight)
        End Operator

        ''' <summary>
        ''' Normalizes two EditorPosition values so the first is before the second
        ''' </summary>
        ''' <param name="vStart">Start position (will be normalized)</param>
        ''' <param name="vEnd">End position (will be normalized)</param>
        Public Shared Sub Normalize(ByRef vStart As EditorPosition, ByRef vEnd As EditorPosition)
            ' Swap if end is before start
            If vEnd.Line < vStart.Line OrElse (vEnd.Line = vStart.Line AndAlso vEnd.Column < vStart.Column) Then
                Dim lTemp As EditorPosition = vStart
                vStart = vEnd
                vEnd = lTemp
            End If
        End Sub
        
        ''' <summary>
        ''' Returns normalized versions of two EditorPosition values without modifying originals
        ''' </summary>
        ''' <param name="vPos1">First position</param>
        ''' <param name="vPos2">Second position</param>
        ''' <returns>Tuple of (earlier position, later position)</returns>
        Public Shared Function GetNormalized(vPos1 As EditorPosition, vPos2 As EditorPosition) As (Start As EditorPosition, [End] As EditorPosition)
            If vPos2.Line < vPos1.Line OrElse (vPos2.Line = vPos1.Line AndAlso vPos2.Column < vPos1.Column) Then
                Return (vPos2, vPos1)
            Else
                Return (vPos1, vPos2)
            End If
        End Function
        
        ''' <summary>
        ''' Compares this position with another to determine which comes first
        ''' </summary>
        ''' <param name="vOther">Position to compare with</param>
        ''' <returns>-1 if this is before other, 0 if equal, 1 if this is after other</returns>
        Public Function CompareTo(vOther As EditorPosition) As Integer
            If Line < vOther.Line Then Return -1
            If Line > vOther.Line Then Return 1
            If Column < vOther.Column Then Return -1
            If Column > vOther.Column Then Return 1
            Return 0
        End Function
        
        ''' <summary>
        ''' Checks if this position is before another position
        ''' </summary>
        ''' <param name="vOther">Position to compare with</param>
        ''' <returns>True if this position comes before the other</returns>
        Public Function IsBefore(vOther As EditorPosition) As Boolean
            Return Line < vOther.Line OrElse (Line = vOther.Line AndAlso Column < vOther.Column)
        End Function
        
        ''' <summary>
        ''' Checks if this position is after another position
        ''' </summary>
        ''' <param name="vOther">Position to compare with</param>
        ''' <returns>True if this position comes after the other</returns>
        Public Function IsAfter(vOther As EditorPosition) As Boolean
            Return Line > vOther.Line OrElse (Line = vOther.Line AndAlso Column > vOther.Column)
        End Function
        
        ''' <summary>
        ''' Checks if this position is between two other positions (inclusive)
        ''' </summary>
        ''' <param name="vStart">Start of range</param>
        ''' <param name="vEnd">End of range</param>
        ''' <returns>True if this position is within the range</returns>
        Public Function IsBetween(vStart As EditorPosition, vEnd As EditorPosition) As Boolean
            Dim lNormalized = GetNormalized(vStart, vEnd)
            Return Not IsBefore(lNormalized.Start) AndAlso Not IsAfter(lNormalized.End)
        End Function

    End Structure

End Namespace
