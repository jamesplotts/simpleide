 
' Models/SyntaxColorSet.vb - Syntax color management
Imports System

Namespace Models

    Public Class SyntaxColorSet
        Public Enum Tags
            eUnknown
            eKeyword
            eType
            eString
            eComment
            eNumber
            eOperator
            ePreprocessor
            eIdentifier
            eSelection
            eLastValue
        End Enum

        Private pvtSyntaxColors(Tags.eLastValue - 1) As String
        Public Event SyntaxColorChanged(vSender As Object, vE As EventArgs)

        Public Property SyntaxColor(vTag As Tags) As String
            Get
                Return pvtSyntaxColors(vTag)
            End Get
            Set(Value As String)
                If vTag < Tags.eUnknown + 1 OrElse vTag > Tags.eLastValue - 1 Then Throw New Exception("Tags Parameter Out Of Range")
                pvtSyntaxColors(vTag) = Value
                RaiseEvent SyntaxColorChanged(CType(Me, Object), New EventArgs())
            End Set
        End Property

        Public ReadOnly Property SyntaxCairoColor(vTag As Tags) As Cairo.Color
            Get
                Return HexToCairoColor(pvtSyntaxColors(vTag))
            End Get
        End Property

        Private Function HexToCairoColor(hex As String) As Cairo.Color
            ' Remove the '#' prefix
            hex = hex.TrimStart("#"c)
            
            ' Parse hex components
            Dim r As Byte = Convert.ToByte(hex.Substring(0, 2), 16)
            Dim g As Byte = Convert.ToByte(hex.Substring(2, 2), 16)
            Dim b As Byte = Convert.ToByte(hex.Substring(4, 2), 16)
            
            ' Convert to Cairo's [0.0, 1.0] range
            Return New Cairo.Color(r / 255.0, g / 255.0, b / 255.0)
        End Function

        Public Function GetColor(vTag As Tags) As String
            Return SyntaxColor(vTag)
        End Function

        Sub New()
            SetDefaults()
        End Sub

        Public Sub SetDefaults()
            pvtSyntaxColors(Tags.eKeyword) = "#d2b48c"
            pvtSyntaxColors(Tags.eType) = "#2B91AF"
            pvtSyntaxColors(Tags.eString) = "#5f9ea0"
            pvtSyntaxColors(Tags.eComment) = "#008000"
            pvtSyntaxColors(Tags.eNumber) = "#5f9ea0"
            pvtSyntaxColors(Tags.eOperator) = "#808080"
            pvtSyntaxColors(Tags.ePreprocessor) = "#808080"
            pvtSyntaxColors(Tags.eIdentifier) = "#FFFFFF"
            pvtSyntaxColors(Tags.eSelection) = "#3399ff"
        End Sub

        Public Function GetTagName(vTag As Tags) As String
            Select Case vTag
                Case Tags.eKeyword : Return "Keyword"
                Case Tags.eType : Return "Type"
                Case Tags.eString : Return "String"
                Case Tags.eComment : Return "Comment"
                Case Tags.eNumber : Return "Number"
                Case Tags.eOperator : Return "Operator"
                Case Tags.ePreprocessor : Return "Preprocessor"
                Case Tags.eIdentifier : Return "Identifier"
                Case Tags.eSelection : Return "Selection"
                Case Else : Throw New Exception("Tags Parameter Out Of Range")
            End Select
        End Function

        ''' <summary>
        ''' Updates all syntax colors from the provided theme
        ''' </summary>
        ''' <param name="vTheme">The EditorTheme containing the new colors to apply</param>
        ''' <remarks>
        ''' This method iterates through all syntax color tags in the theme's SyntaxColors dictionary
        ''' and updates the corresponding colors in this SyntaxColorSet
        ''' </remarks>
        Public Sub UpdateFromTheme(vTheme As EditorTheme)
            Try
                ' Validate theme parameter
                If vTheme Is Nothing Then
                    Console.WriteLine("UpdateFromTheme: Theme is Nothing")
                    Return
                End If
                
                ' Validate theme has syntax colors
                If vTheme.SyntaxColors Is Nothing Then
                    Console.WriteLine("UpdateFromTheme: Theme.SyntaxColors is Nothing")
                    Return
                End If
                
                ' Update each syntax color from the theme
                For Each kvp In vTheme.SyntaxColors
                    ' Only update valid tag values
                    If kvp.Key >= Tags.eKeyword AndAlso kvp.Key <= Tags.eSelection Then
                        ' Update the color using the property setter which will trigger events
                        Me.SyntaxColor(kvp.Key) = kvp.Value
                        
                        Console.WriteLine($"UpdateFromTheme: Updated {kvp.Key} to {kvp.Value}")
                    End If
                Next
                
                ' Trigger a final color changed event to ensure UI updates
                RaiseEvent SyntaxColorChanged(Me, New EventArgs())
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFromTheme error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
