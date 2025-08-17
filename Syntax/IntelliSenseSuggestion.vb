' IntelliSenseSuggestion.vb
' Created: 2025-08-12 20:59:10

Imports System



Namespace Syntax

    
    ' IntelliSense suggestion item
    Public Class IntelliSenseSuggestion
        Public Property Text As String = ""              ' Text to insert
        Public Property DisplayText As String = ""       ' Text to display in list
        Public Property Description As String = ""       ' Tooltip Description
        Public Property Icon As String = ""             ' Icon Identifier
        Public Property SuggestionType As IntelliSenseSuggestionType = IntelliSenseSuggestionType.eOther
        Public Property Signature As String = ""        ' Method Signature
        Public Property Data As Object                  ' Associated Data (MemberInfo, DocumentNode, etc.)

        ' Name is an alias for Text
        Public Property Name As String
            Get
                Return Text
            End Get
            Set(Value As String)
                Text = Value
            End Set
        End Property
        
        ' TypeName is an alias for Description or extracted from Signature
        Public Property TypeName As String
            Get
                ' If we have a signature, extract the type from it
                If Not String.IsNullOrEmpty(Signature) Then
                    ' For methods: "MethodName(params) As ReturnType"
                    Dim lAsIndex As Integer = Signature.LastIndexOf(" As ")
                    If lAsIndex > 0 Then
                        Return Signature.Substring(lAsIndex + 4).Trim()
                    End If
                End If
                
                ' Otherwise use description
                Return Description
            End Get
            Set(Value As String)
                Description = Value
            End Set
        End Property
        
        ' Kind is mapped from Type enum
        Public ReadOnly Property Kind As IntelliSenseSuggestionKind
            Get
                Select Case SuggestionType
                    Case IntelliSenseSuggestionType.eKeyword
                        Return IntelliSenseSuggestionKind.eKeyword
                    Case IntelliSenseSuggestionType.eType
                        Return IntelliSenseSuggestionKind.eClass
                    Case IntelliSenseSuggestionType.eNamespace
                        Return IntelliSenseSuggestionKind.eNamespace
                    Case IntelliSenseSuggestionType.eMethod
                        Return IntelliSenseSuggestionKind.eMethod
                    Case IntelliSenseSuggestionType.eProperty
                        Return IntelliSenseSuggestionKind.eProperty
                    Case IntelliSenseSuggestionType.eField
                        Return IntelliSenseSuggestionKind.eField
                    Case IntelliSenseSuggestionType.eEvent
                        Return IntelliSenseSuggestionKind.eEvent
                    Case IntelliSenseSuggestionType.eVariable
                        Return IntelliSenseSuggestionKind.eLocalVariable
                    Case IntelliSenseSuggestionType.eParameter
                        Return IntelliSenseSuggestionKind.eParameter
                    Case IntelliSenseSuggestionType.eSnippet
                        Return IntelliSenseSuggestionKind.eSnippet
                    Case Else
                        Return IntelliSenseSuggestionKind.eOther
                End Select
            End Get
        End Property

        Private pPriority As Integer = 50
        
        ''' <summary>
        ''' Priority for sorting suggestions (higher = more relevant)
        ''' </summary>
        Public Property Priority As Integer
            Get
                Return pPriority
            End Get
            Set(value As Integer)
                pPriority = value
            End Set
        End Property
        
        ''' <summary>
        ''' Calculate priority based on suggestion type
        ''' </summary>
        Public Sub CalculatePriority()
            Select Case SuggestionType
                Case IntelliSenseSuggestionType.eProperty
                    pPriority = 100
                Case IntelliSenseSuggestionType.eMethod
                    pPriority = 90
                Case IntelliSenseSuggestionType.eField
                    pPriority = 80
                Case IntelliSenseSuggestionType.eType
                    pPriority = 70
                Case IntelliSenseSuggestionType.eKeyword
                    pPriority = 60
                Case IntelliSenseSuggestionType.eNamespace
                    pPriority = 50
                Case Else
                    pPriority = 40
            End Select
        End Sub
        
        ''' <summary>
        ''' Comparison function for sorting
        ''' </summary>
        Public Shared Function CompareByPriority(vA As IntelliSenseSuggestion, vB As IntelliSenseSuggestion) As Integer
            ' Sort by priority first (descending)
            Dim lPriorityCompare As Integer = vB.Priority.CompareTo(vA.Priority)
            If lPriorityCompare <> 0 Then
                Return lPriorityCompare
            End If
            
            ' Then by name (ascending)
            Return String.Compare(vA.Text, vB.Text, StringComparison.OrdinalIgnoreCase)
        End Function
        
    End Class
End Namespace
