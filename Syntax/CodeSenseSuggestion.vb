' CodeSenseSuggestion.vb
' Created: 2025-08-12 20:59:10

Imports System



Namespace Syntax

    
    ' CodeSense suggestion item
    Public Class CodeSenseSuggestion
        Public Property Text As String = ""              ' Text to insert
        Public Property DisplayText As String = ""       ' Text to display in list
        Public Property Description As String = ""       ' Tooltip Description
        Public Property Icon As String = ""             ' Icon Identifier
        Public Property SuggestionType As CodeSenseSuggestionType = CodeSenseSuggestionType.eOther
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
        Public Property Kind As CodeSenseSuggestionKind
            Get
                Select Case SuggestionType
                    Case CodeSenseSuggestionType.eKeyword
                        Return CodeSenseSuggestionKind.eKeyword
                    Case CodeSenseSuggestionType.eType
                        Return CodeSenseSuggestionKind.eClass
                    Case CodeSenseSuggestionType.eNamespace
                        Return CodeSenseSuggestionKind.eNamespace
                    Case CodeSenseSuggestionType.eMethod
                        Return CodeSenseSuggestionKind.eMethod
                    Case CodeSenseSuggestionType.eProperty
                        Return CodeSenseSuggestionKind.eProperty
                    Case CodeSenseSuggestionType.eField
                        Return CodeSenseSuggestionKind.eField
                    Case CodeSenseSuggestionType.eEvent
                        Return CodeSenseSuggestionKind.eEvent
                    Case CodeSenseSuggestionType.eVariable
                        Return CodeSenseSuggestionKind.eLocalVariable
                    Case CodeSenseSuggestionType.eParameter
                        Return CodeSenseSuggestionKind.eParameter
                    Case CodeSenseSuggestionType.eSnippet
                        Return CodeSenseSuggestionKind.eSnippet
                    Case Else
                        Return CodeSenseSuggestionKind.eOther
                End Select
            End Get
            Set(value as CodeSenseSuggestionKind)
                Select Case value
                    Case CodeSenseSuggestionKind.eKeyword
                        SuggestionType = CodeSenseSuggestionType.eKeyword
                    Case CodeSenseSuggestionKind.eClass
                        SuggestionType = CodeSenseSuggestionType.eType
                    Case CodeSenseSuggestionKind.eNamespace
                        SuggestionType = CodeSenseSuggestionType.eNamespace
                    Case CodeSenseSuggestionKind.eMethod
                        SuggestionType = CodeSenseSuggestionType.eMethod
                    Case CodeSenseSuggestionKind.eProperty
                        SuggestionType = CodeSenseSuggestionType.eProperty
                    Case CodeSenseSuggestionKind.eField
                        SuggestionType = CodeSenseSuggestionType.eField
                    Case CodeSenseSuggestionKind.eEvent
                        SuggestionType = CodeSenseSuggestionType.eEvent
                    Case CodeSenseSuggestionKind.eLocalVariable
                        SuggestionType = CodeSenseSuggestionType.eVariable
                    Case CodeSenseSuggestionKind.eParameter
                        SuggestionType = CodeSenseSuggestionType.eParameter
                    Case CodeSenseSuggestionKind.eSnippet
                        SuggestionType = CodeSenseSuggestionType.eSnippet
                    Case Else
                        SuggestionType = CodeSenseSuggestionType.eOther
                End Select

            End Set
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
                Case CodeSenseSuggestionType.eProperty
                    pPriority = 100
                Case CodeSenseSuggestionType.eMethod
                    pPriority = 90
                Case CodeSenseSuggestionType.eField
                    pPriority = 80
                Case CodeSenseSuggestionType.eType
                    pPriority = 70
                Case CodeSenseSuggestionType.eKeyword
                    pPriority = 60
                Case CodeSenseSuggestionType.eNamespace
                    pPriority = 50
                Case Else
                    pPriority = 40
            End Select
        End Sub
        
        ''' <summary>
        ''' Comparison function for sorting
        ''' </summary>
        Public Shared Function CompareByPriority(vA As CodeSenseSuggestion, vB As CodeSenseSuggestion) As Integer
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
