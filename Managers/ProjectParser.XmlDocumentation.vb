' ProjectParser.XmlDocumentation.vb - XML documentation parsing for ProjectParser
' Created: 2025-08-26

Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Managers
    
    ''' <summary>
    ''' ProjectParser extension for handling XML documentation comments
    ''' </summary>
    Partial Public Class ProjectParser
        
        ' ===== XML Documentation Classes =====
        

        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Extracts XML documentation for a node at tXmlDocInfohe specified line in the current file content
        ''' </summary>
        ''' <param name="vLines">The lines of the current file being parsed</param>
        ''' <param name="vDeclarationLine">The line where the declaration starts (0-based)</param>
        ''' <returns>True if documentation was found and applied</returns>
        Public Function ExtractAndApplyXmlDocumentation(vLines As List(Of String), vDeclarationLine As Integer, vNode As SyntaxNode) As Boolean
            Try
                If vLines Is Nothing OrElse vDeclarationLine < 0 OrElse vDeclarationLine >= vLines.Count Then
                    Return False
                End If
                
                ' Extract the XML documentation
                Dim lDoc As XmlDocInfo = ExtractXmlDocumentation(vLines, vDeclarationLine)
                
                ' Apply to the node if documentation was found
                If HasDocumentation(lDoc) Then
                    ApplyXmlDocumentation(vNode, lDoc)
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"ExtractAndApplyXmlDocumentation error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Private Methods - XML Documentation Extraction =====
        
        ''' <summary>
        ''' Extract XML documentation for a declaration at the given line
        ''' </summary>
        ''' <param name="vLines">All lines in the file</param>
        ''' <param name="vStartLine">The line where the declaration starts (0-based)</param>
        Private Function ExtractXmlDocumentation(vLines As List(Of String), vStartLine As Integer) As XmlDocInfo
            Try
                Dim lDoc As New XmlDocInfo()
                
                ' Look backwards from the declaration line for XML doc comments
                Dim lCurrentLine As Integer = vStartLine - 1
                Dim lDocLines As New List(Of String)()
                
                While lCurrentLine >= 0
                    Dim lLine As String = vLines(lCurrentLine).Trim()
                    
                    ' Check if this is an XML doc comment line
                    If lLine.StartsWith("'''") Then
                        ' Remove the ''' prefix and any leading space
                        Dim lDocText As String = lLine.Substring(3)
                        If lDocText.StartsWith(" ") Then
                            lDocText = lDocText.Substring(1)
                        End If
                        lDocLines.Insert(0, lDocText) ' Insert at beginning to maintain order
                        lCurrentLine -= 1
                    Else
                        ' Stop if we hit a non-doc comment line
                        Exit While
                    End If
                End While
                
                ' Parse the collected XML documentation
                If lDocLines.Count > 0 Then
                    ParseXmlDocLines(lDocLines, lDoc)
                End If
                
                Return lDoc
                
            Catch ex As Exception
                Console.WriteLine($"ExtractXmlDocumentation error: {ex.Message}")
                Return New XmlDocInfo()
            End Try
        End Function
        
        ''' <summary>
        ''' Parse XML documentation lines into structured data
        ''' </summary>
        Private Sub ParseXmlDocLines(vLines As List(Of String), vDoc As XmlDocInfo)
            Try
                Dim lCurrentTag As String = ""
                Dim lCurrentContent As New List(Of String)()
                Dim lCurrentParamName As String = ""
                Dim lCurrentExceptionType As String = ""
                
                For Each lLine In vLines
                    ' Check for XML tag opening
                    Dim lTagMatch As Match = Regex.Match(lLine, "^<(\w+)(?:\s+(\w+)=""([^""]+)"")?>(.*)$")
                    
                    If lTagMatch.Success Then
                        ' Process previous tag if any
                        If Not String.IsNullOrEmpty(lCurrentTag) Then
                            ProcessXmlTag(lCurrentTag, lCurrentContent, vDoc, lCurrentParamName, lCurrentExceptionType)
                        End If
                        
                        ' Start new tag
                        lCurrentTag = lTagMatch.Groups(1).Value.ToLower()
                        lCurrentContent.Clear()
                        
                        ' Handle attributes
                        If lTagMatch.Groups(2).Success Then
                            Dim lAttrName As String = lTagMatch.Groups(2).Value
                            Dim lAttrValue As String = lTagMatch.Groups(3).Value
                            
                            If lCurrentTag = "param" AndAlso lAttrName = "name" Then
                                lCurrentParamName = lAttrValue
                            ElseIf lCurrentTag = "exception" AndAlso lAttrName = "cref" Then
                                lCurrentExceptionType = lAttrValue
                            End If
                        End If
                        
                        ' Check if tag has content on same line
                        Dim lRemainder As String = lTagMatch.Groups(4).Value
                        Dim lCloseMatch As Match = Regex.Match(lRemainder, "^(.*?)</\w+>(.*)$")
                        
                        If lCloseMatch.Success Then
                            ' Single-line tag
                            lCurrentContent.Add(lCloseMatch.Groups(1).Value.Trim())
                            ProcessXmlTag(lCurrentTag, lCurrentContent, vDoc, lCurrentParamName, lCurrentExceptionType)
                            lCurrentTag = ""
                            lCurrentContent.Clear()
                            lCurrentParamName = ""
                            lCurrentExceptionType = ""
                            
                            ' Process any remaining content
                            If Not String.IsNullOrEmpty(lCloseMatch.Groups(2).Value.Trim()) Then
                                lCurrentContent.Add(lCloseMatch.Groups(2).Value.Trim())
                            End If
                        Else
                            ' Multi-line tag - add remainder if not empty
                            If Not String.IsNullOrEmpty(lRemainder.Trim()) Then
                                lCurrentContent.Add(lRemainder.Trim())
                            End If
                        End If
                    Else
                        ' Check for closing tag
                        Dim lCloseMatch As Match = Regex.Match(lLine, "^(.*?)</(\w+)>(.*)$")
                        If lCloseMatch.Success Then
                            ' Add content before closing tag
                            If Not String.IsNullOrEmpty(lCloseMatch.Groups(1).Value.Trim()) Then
                                lCurrentContent.Add(lCloseMatch.Groups(1).Value.Trim())
                            End If
                            
                            ' Process the tag
                            ProcessXmlTag(lCurrentTag, lCurrentContent, vDoc, lCurrentParamName, lCurrentExceptionType)
                            lCurrentTag = ""
                            lCurrentContent.Clear()
                            lCurrentParamName = ""
                            lCurrentExceptionType = ""
                            
                            ' Handle any content after closing tag
                            If Not String.IsNullOrEmpty(lCloseMatch.Groups(3).Value.Trim()) Then
                                lCurrentContent.Add(lCloseMatch.Groups(3).Value.Trim())
                            End If
                        Else
                            ' Content line - add to current tag
                            If Not String.IsNullOrEmpty(lLine.Trim()) Then
                                lCurrentContent.Add(lLine.Trim())
                            End If
                        End If
                    End If
                Next
                
                ' Process any remaining tag
                If Not String.IsNullOrEmpty(lCurrentTag) Then
                    ProcessXmlTag(lCurrentTag, lCurrentContent, vDoc, lCurrentParamName, lCurrentExceptionType)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseXmlDocLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Process a parsed XML tag and store in XmlDocInfo
        ''' </summary>
        Private Sub ProcessXmlTag(vTag As String, vContent As List(Of String), vDoc As XmlDocInfo, 
                                  vParamName As String, vExceptionType As String)
            Try
                Dim lContent As String = String.Join(" ", vContent).Trim()
                
                Select Case vTag.ToLower()
                    Case "summary"
                        vDoc.Summary = lContent
                        
                    Case "param"
                        If Not String.IsNullOrEmpty(vParamName) AndAlso Not String.IsNullOrEmpty(lContent) Then
                            vDoc.Parameters(vParamName) = lContent
                        End If
                        
                    Case "returns", "Return"
                        vDoc.Returns = lContent
                        
                    Case "remarks"
                        vDoc.Remarks = lContent
                        
                    Case "example"
                        ' Preserve line breaks in examples
                        vDoc.Example = String.Join(Environment.NewLine, vContent)
                        
                    Case "exception"
                        If Not String.IsNullOrEmpty(vExceptionType) AndAlso Not String.IsNullOrEmpty(lContent) Then
                            vDoc.Exceptions(vExceptionType) = lContent
                        End If
                        
                    Case "seealso"
                        If Not String.IsNullOrEmpty(lContent) Then
                            vDoc.SeeAlso.Add(lContent)
                        End If
                        
                    Case "value"
                        vDoc.Value = lContent
                End Select
                
            Catch ex As Exception
                Console.WriteLine($"ProcessXmlTag error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Apply XML documentation to a SyntaxNode
        ''' </summary>
        Private Sub ApplyXmlDocumentation(vNode As SyntaxNode, vDoc As XmlDocInfo)
            Try
                ' Apply basic documentation
                vNode.Summary = vDoc.Summary
                vNode.Returns = vDoc.Returns
                vNode.Remarks = vDoc.Remarks
                vNode.Example = vDoc.Example
                
                ' Apply parameter documentation
                If vDoc.Parameters.Count > 0 Then
                    If vNode.ParamDocs Is Nothing Then
                        vNode.ParamDocs = New Dictionary(Of String, String)()
                    End If
                    For Each lKvp In vDoc.Parameters
                        vNode.ParamDocs(lKvp.Key) = lKvp.Value
                    Next
                End If
                
                ' Store exceptions and see also in attributes if needed
                If vDoc.Exceptions.Count > 0 OrElse vDoc.SeeAlso.Count > 0 OrElse Not String.IsNullOrEmpty(vDoc.Value) Then
                    If vNode.Attributes Is Nothing Then
                        vNode.Attributes = New Dictionary(Of String, String)()
                    End If
                    
                    ' Store exceptions as JSON or delimited string
                    If vDoc.Exceptions.Count > 0 Then
                        Dim lExceptionList As New List(Of String)()
                        For Each lKvp In vDoc.Exceptions
                            lExceptionList.Add($"{lKvp.Key}: {lKvp.Value}")
                        Next
                        vNode.Attributes("Exceptions") = String.Join("|", lExceptionList)
                    End If
                    
                    ' Store see also references
                    If vDoc.SeeAlso.Count > 0 Then
                        vNode.Attributes("SeeAlso") = String.Join("|", vDoc.SeeAlso)
                    End If
                    
                    ' Store value documentation for properties
                    If Not String.IsNullOrEmpty(vDoc.Value) Then
                        vNode.Attributes("Value") = vDoc.Value
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplyXmlDocumentation error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Check if XmlDocInfo has any documentation
        ''' </summary>
        Private Function HasDocumentation(vDoc As XmlDocInfo) As Boolean
            Return Not String.IsNullOrWhiteSpace(vDoc.Summary) OrElse
                   Not String.IsNullOrWhiteSpace(vDoc.Returns) OrElse
                   Not String.IsNullOrWhiteSpace(vDoc.Remarks) OrElse
                   Not String.IsNullOrWhiteSpace(vDoc.Example) OrElse
                   Not String.IsNullOrWhiteSpace(vDoc.Value) OrElse
                   vDoc.Parameters.Count > 0 OrElse
                   vDoc.Exceptions.Count > 0 OrElse
                   vDoc.SeeAlso.Count > 0
        End Function
        
        ' ===== Integration with Parsing Methods =====
        
        ''' <summary>
        ''' Enhanced method parsing that includes XML documentation extraction
        ''' </summary>
        Private Function ParseMethodWithDocumentation(vLines As List(Of String), vLineIndex As Integer, vTypeNode As SyntaxNode, vMethodNode As SyntaxNode) As Boolean
            Try
                ' Extract and apply XML documentation if present
                ExtractAndApplyXmlDocumentation(vLines, vLineIndex, vMethodNode)
                
                ' Continue with normal method parsing...
                ' (The calling code will handle the actual method parsing)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ParseMethodWithDocumentation error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Enhanced property parsing that includes XML documentation extraction
        ''' </summary>
        Private Function ParsePropertyWithDocumentation(vLines As List(Of String), vLineIndex As Integer,
                                                       vTypeNode As SyntaxNode, vPropertyNode As SyntaxNode) As Boolean
            Try
                ' Extract and apply XML documentation if present
                ExtractAndApplyXmlDocumentation(vLines, vLineIndex, vPropertyNode)
                
                ' Continue with normal property parsing...
                ' (The calling code will handle the actual property parsing)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ParsePropertyWithDocumentation error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Enhanced type parsing that includes XML documentation extraction
        ''' </summary>
        Private Function ParseTypeWithDocumentation(vLines As List(Of String), vLineIndex As Integer, vParentNode As SyntaxNode, vTypeNode As SyntaxNode) As Boolean
            Try
                ' Extract and apply XML documentation if present
                ExtractAndApplyXmlDocumentation(vLines, vLineIndex, vTypeNode)
                
                ' Continue with normal type parsing...
                ' (The calling code will handle the actual type parsing)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ParseTypeWithDocumentation error: {ex.Message}")
                Return False
            End Try
        End Function
        
    End Class
    
End Namespace
