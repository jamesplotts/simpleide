' Syntax/VBParser.XmlDocumentation.vb - Parse XML documentation comments for CodeSense

Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models

' VBParser.XmlDocumentation.vb
' Created: 2025-08-11 16:55:44

Namespace Syntax
    
    Partial Public Class VBParser
        
        ''' <summary>
        ''' Extract XML documentation for a declaration at the given line
        ''' </summary>
        Private Function ExtractXmlDocumentation(vStartLine As Integer) As XmlDocInfo
            Try
                Dim lDoc As New XmlDocInfo()
                
                ' Look backwards from the declaration line for XML doc comments
                Dim lCurrentLine As Integer = vStartLine - 1
                Dim lDocLines As New List(Of String)()
                
                While lCurrentLine >= 0
                    Dim lLine As String = pLines(lCurrentLine).Trim()
                    
                    ' Check if this is an XML doc comment line
                    If lLine.StartsWith("'''") Then
                        ' Remove the ''' prefix
                        Dim lDocText As String = lLine.Substring(3).Trim()
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
                
                For Each lLine In vLines
                    ' Check for XML tags
                    If lLine.StartsWith("<summary>") Then
                        lCurrentTag = "summary"
                        Dim lContent As String = lLine.Replace("<summary>", "").Replace("</summary>", "").Trim()
                        If Not String.IsNullOrEmpty(lContent) Then
                            lCurrentContent.Add(lContent)
                        End If
                        
                    ElseIf lLine.StartsWith("</summary>") Then
                        vDoc.Summary = String.Join(" ", lCurrentContent)
                        lCurrentContent.Clear()
                        lCurrentTag = ""
                        
                    ElseIf lLine.StartsWith("<param name=""") Then
                        ' Extract parameter name
                        Dim lMatch As Match = Regex.Match(lLine, "<param name=""([^""]+)"">(.*)$")
                        If lMatch.Success Then
                            lCurrentParamName = lMatch.Groups(1).Value
                            lCurrentTag = "param"
                            Dim lContent As String = lMatch.Groups(2).Value.Replace("</param>", "").Trim()
                            If Not String.IsNullOrEmpty(lContent) Then
                                lCurrentContent.Add(lContent)
                            End If
                        End If
                        
                    ElseIf lLine.StartsWith("</param>") Then
                        If Not String.IsNullOrEmpty(lCurrentParamName) Then
                            vDoc.Parameters(lCurrentParamName) = String.Join(" ", lCurrentContent)
                        End If
                        lCurrentContent.Clear()
                        lCurrentTag = ""
                        lCurrentParamName = ""
                        
                    ElseIf lLine.StartsWith("<returns>") Then
                        lCurrentTag = "returns"
                        Dim lContent As String = lLine.Replace("<returns>", "").Replace("</returns>", "").Trim()
                        If Not String.IsNullOrEmpty(lContent) Then
                            lCurrentContent.Add(lContent)
                        End If
                        
                    ElseIf lLine.StartsWith("</returns>") Then
                        vDoc.Returns = String.Join(" ", lCurrentContent)
                        lCurrentContent.Clear()
                        lCurrentTag = ""
                        
                    ElseIf lLine.StartsWith("<remarks>") Then
                        lCurrentTag = "remarks"
                        Dim lContent As String = lLine.Replace("<remarks>", "").Replace("</remarks>", "").Trim()
                        If Not String.IsNullOrEmpty(lContent) Then
                            lCurrentContent.Add(lContent)
                        End If
                        
                    ElseIf lLine.StartsWith("</remarks>") Then
                        vDoc.Remarks = String.Join(" ", lCurrentContent)
                        lCurrentContent.Clear()
                        lCurrentTag = ""
                        
                    ElseIf lLine.StartsWith("<example>") Then
                        lCurrentTag = "example"
                        Dim lContent As String = lLine.Replace("<example>", "").Replace("</example>", "").Trim()
                        If Not String.IsNullOrEmpty(lContent) Then
                            lCurrentContent.Add(lContent)
                        End If
                        
                    ElseIf lLine.StartsWith("</example>") Then
                        vDoc.Example = String.Join(Environment.NewLine, lCurrentContent)
                        lCurrentContent.Clear()
                        lCurrentTag = ""
                        
                    ElseIf Not String.IsNullOrEmpty(lCurrentTag) Then
                        ' Continuation of current tag
                        lCurrentContent.Add(lLine)
                    End If
                Next
                
                ' Handle any unclosed tags
                If lCurrentContent.Count > 0 Then
                    Select Case lCurrentTag
                        Case "summary"
                            vDoc.Summary = String.Join(" ", lCurrentContent)
                        Case "param"
                            If Not String.IsNullOrEmpty(lCurrentParamName) Then
                                vDoc.Parameters(lCurrentParamName) = String.Join(" ", lCurrentContent)
                            End If
                        Case "returns"
                            vDoc.Returns = String.Join(" ", lCurrentContent)
                        Case "remarks"
                            vDoc.Remarks = String.Join(" ", lCurrentContent)
                        Case "example"
                            vDoc.Example = String.Join(Environment.NewLine, lCurrentContent)
                    End Select
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseXmlDocLines error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Helper class to hold XML documentation
        ''' </summary>
        Private Class XmlDocInfo
            Public Property Summary As String = ""
            Public Property Parameters As New Dictionary(Of String, String)()
            Public Property Returns As String = ""
            Public Property Remarks As String = ""
            Public Property Example As String = ""
        End Class
        
        ''' <summary>
        ''' Apply XML documentation to a SyntaxNode
        ''' </summary>
        Private Sub ApplyXmlDocumentation(vNode As SyntaxNode, vDoc As XmlDocInfo)
            Try
                vNode.Summary = vDoc.Summary
                vNode.Returns = vDoc.Returns
                vNode.Remarks = vDoc.Remarks
                vNode.Example = vDoc.Example
                
                ' Apply parameter documentation
                If vDoc.Parameters.Count > 0 Then
                    vNode.ParamDocs = New Dictionary(Of String, String)()
                    For Each lKvp In vDoc.Parameters
                        vNode.ParamDocs(lKvp.Key) = lKvp.Value
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ApplyXmlDocumentation error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
