' Managers/ProjectManager.SymbolIndex.vb - Fast bare-name symbol index and Imports-derived
' fully-qualified-name candidate resolution
' Created: 2026-07-28
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Managers

    Partial Public Class ProjectManager

        ''' <summary>
        ''' All project-defined definition nodes (classes/methods/properties/fields/etc.),
        ''' keyed by their bare (unqualified) name - lets a lookup by name go straight to the
        ''' small set of same-named candidates instead of walking every file's tree
        ''' </summary>
        Private pSymbolIndex As New Dictionary(Of String, List(Of SyntaxNode))(StringComparer.OrdinalIgnoreCase)

        ''' <summary>
        ''' Which bare names each file most recently contributed to pSymbolIndex, so that
        ''' file's entries can be removed precisely - not by scanning the whole index - the
        ''' next time it's reparsed
        ''' </summary>
        Private pSymbolIndexByFile As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)

        Private ReadOnly pSymbolIndexLock As New Object()

        ''' <summary>
        ''' Rebuilds vFile's contribution to the project-wide symbol index from its current
        ''' syntax tree
        ''' </summary>
        ''' <remarks>
        ''' Called after every (re)parse of a file (both the single-file live-editing path and
        ''' the whole-project initial load), so the index stays current without ever needing a
        ''' whole-project rescan of its own - cost is bounded to this one file's own node count,
        ''' the same incremental-not-whole-project principle the reparse work itself follows
        ''' </remarks>
        Public Sub ReindexFile(vFile As SourceFileInfo)
            Try
                If vFile Is Nothing OrElse String.IsNullOrEmpty(vFile.FilePath) Then Return

                SyncLock pSymbolIndexLock
                    ' Remove this file's previous contribution, if any
                    Dim lPreviousNames As List(Of String) = Nothing
                    If pSymbolIndexByFile.TryGetValue(vFile.FilePath, lPreviousNames) Then
                        for each lName in lPreviousNames
                            Dim lBucket As List(Of SyntaxNode) = Nothing
                            If pSymbolIndex.TryGetValue(lName, lBucket) Then
                                lBucket.RemoveAll(Function(n) String.Equals(n.FilePath, vFile.FilePath, StringComparison.OrdinalIgnoreCase))
                                If lBucket.Count = 0 Then pSymbolIndex.Remove(lName)
                            End If
                        Next
                        pSymbolIndexByFile.Remove(vFile.FilePath)
                    End If

                    If vFile.SyntaxTree Is Nothing Then Return

                    ' Add its current contribution
                    Dim lFound As New List(Of SyntaxNode)
                    CollectDefinitionNodes(vFile.SyntaxTree, lFound)

                    Dim lNewNames As New List(Of String)
                    for each lNode in lFound
                        If String.IsNullOrEmpty(lNode.Name) Then Continue for

                        Dim lBucket As List(Of SyntaxNode) = Nothing
                        If Not pSymbolIndex.TryGetValue(lNode.Name, lBucket) Then
                            lBucket = New List(Of SyntaxNode)
                            pSymbolIndex(lNode.Name) = lBucket
                        End If
                        lBucket.Add(lNode)
                        lNewNames.Add(lNode.Name)
                    Next
                    pSymbolIndexByFile(vFile.FilePath) = lNewNames
                End SyncLock

            Catch ex As Exception
                Console.WriteLine($"ReindexFile error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Recursively collects every definition-type node (class/method/property/field/etc.,
        ''' per IsDefinitionNode) under vNode
        ''' </summary>
        Private Sub CollectDefinitionNodes(vNode As SyntaxNode, vResults As List(Of SyntaxNode))
            If vNode Is Nothing Then Return
            Try
                If IsDefinitionNode(vNode) Then vResults.Add(vNode)

                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        CollectDefinitionNodes(lChild, vResults)
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"CollectDefinitionNodes error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Builds the bounded, deterministic list of fully-qualified names vBareName could
        ''' legally resolve to from vFilePath: the file's own namespace first (VB.NET resolves
        ''' same-namespace names before imported ones), then one candidate per Imports
        ''' statement, in the order they're written in the file
        ''' </summary>
        ''' <param name="vFilePath">The file the identifier was clicked in</param>
        ''' <param name="vBareName">The unqualified identifier text</param>
        ''' <returns>Candidate fully-qualified names, most-likely-first; empty if vFilePath has no parsed tree to read Imports/namespace from</returns>
        Public Function GetImportsDerivedCandidates(vFilePath As String, vBareName As String) As List(Of String)
            Dim lCandidates As New List(Of String)
            Try
                If String.IsNullOrEmpty(vFilePath) OrElse String.IsNullOrEmpty(vBareName) Then Return lCandidates
                If pSourceFiles Is Nothing Then Return lCandidates

                Dim lFile As SourceFileInfo = Nothing
                If Not pSourceFiles.TryGetValue(vFilePath, lFile) OrElse lFile.SyntaxTree Is Nothing Then Return lCandidates
                If lFile.SyntaxTree.Children Is Nothing Then Return lCandidates

                Dim lRootNamespace As String = GetEffectiveRootNamespace()

                ' The file's own namespace - this codebase's convention is at most one
                ' top-level Namespace block per file (see CLAUDE.md), so the first one found
                ' at the file root is it
                Dim lOwnNamespace As String = Nothing
                for each lChild As SyntaxNode in lFile.SyntaxTree.Children
                    If lChild.NodeType = CodeNodeType.eNamespace Then
                        lOwnNamespace = lChild.Name
                        Exit for
                    End If
                Next

                Dim lOwnNamespaceFqn As String = If(String.IsNullOrEmpty(lOwnNamespace), lRootNamespace, $"{lRootNamespace}.{lOwnNamespace}")
                lCandidates.Add($"{lOwnNamespaceFqn}.{vBareName}")

                for each lChild As SyntaxNode in lFile.SyntaxTree.Children
                    If lChild.NodeType = CodeNodeType.eImport AndAlso Not String.IsNullOrEmpty(lChild.Name) Then
                        lCandidates.Add($"{lChild.Name}.{vBareName}")
                    End If
                Next

                ' Project-level imports (<Import Include="..."> in the .vbproj) are implicitly
                ' in scope for every file, same as an in-source Imports statement would be, but
                ' aren't part of any file's own SyntaxTree - added last since an explicit
                ' in-file Imports is the more likely match
                If pCurrentProjectInfo?.ProjectImports IsNot Nothing Then
                    for each lProjectImport in pCurrentProjectInfo.ProjectImports
                        If Not String.IsNullOrEmpty(lProjectImport) Then
                            lCandidates.Add($"{lProjectImport}.{vBareName}")
                        End If
                    Next
                End If

            Catch ex As Exception
                Console.WriteLine($"GetImportsDerivedCandidates error: {ex.Message}")
            End Try
            Return lCandidates
        End Function

        ''' <summary>
        ''' Looks vBareName up in the symbol index and returns the first entry whose fully-
        ''' qualified name matches one of vCandidates, in candidate order
        ''' </summary>
        Private Function FindDefinitionByFqnCandidates(vBareName As String, vCandidates As List(Of String)) As DefinitionInfo
            Try
                If String.IsNullOrEmpty(vBareName) OrElse vCandidates Is Nothing OrElse vCandidates.Count = 0 Then Return Nothing

                Dim lBucket As List(Of SyntaxNode) = Nothing
                SyncLock pSymbolIndexLock
                    If Not pSymbolIndex.TryGetValue(vBareName, lBucket) Then Return Nothing
                    lBucket = New List(Of SyntaxNode)(lBucket) ' snapshot while still under the lock
                End SyncLock

                Dim lRootNamespace As String = GetEffectiveRootNamespace()

                for each lCandidate in vCandidates
                    for each lNode in lBucket
                        Dim lNodeFqn As String = $"{lRootNamespace}.{lNode.GetFullyQualifiedName()}"
                        If String.Equals(lNodeFqn, lCandidate, StringComparison.OrdinalIgnoreCase) Then
                            Return New DefinitionInfo(lNode, lNode.FilePath)
                        End If
                    Next
                Next

                Return Nothing

            Catch ex As Exception
                Console.WriteLine($"FindDefinitionByFqnCandidates error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Gets the effective root namespace for the current project
        ''' </summary>
        Private Function GetEffectiveRootNamespace() As String
            Return If(pCurrentProjectInfo?.GetEffectiveRootNamespace(), "SimpleIDE")
        End Function

    End Class

End Namespace
