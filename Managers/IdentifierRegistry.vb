' Managers/IdentifierRegistry.vb - Comprehensive in-memory identifier tracking with instant reference access
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports SimpleIDE.Models
Imports SimpleIDE.Interfaces

' IdentifierRegistry.vb
' Created: 2025-08-07 23:20:42

Namespace Managers
    
    ''' <summary>
    ''' Comprehensive in-memory registry of all identifiers with instant reference tracking
    ''' </summary>
    Partial Public Class IdentifierRegistry
        
        ' ===== Enums =====
        
        Public Enum IdentifierScope
            eUnspecified
            eLocal          ' Local variable
            eParameter      ' Method Parameter
            eField          ' Class field
            eProperty       ' Property
            eMethod         ' Method/Function/Sub
            eClass          ' Class/Module/Structure
            eInterface      ' Interface
            eEnum           ' Enumeration
            eEnumMember     ' Enum member
            eConstant       ' Constant
            eEvent          ' Event
            eDelegate       ' Delegate
            eNamespace      ' Namespace
            eLastValue
        End Enum
        
        Public Enum ReferenceKind
            eUnspecified
            eDeclaration    ' the Declaration itself
            eUsage          ' Normal Usage/Reference
            eMethodCall     ' Method invocation
            eTypeReference  ' Type Usage (As TypeName)
            eInheritance    ' Inherits/Implements
            eAssignment     ' Left side of assignment
            eRead           ' Right side/read access
            eLastValue
        End Enum
        
        ' ===== Classes =====
        
        ''' <summary>
        ''' Complete information about an identifier declaration
        ''' </summary>
        Public Class DeclarationInfo
            Public Property Identifier As String           ' Original case-sensitive Name
            Public Property CanonicalCase As String        ' the official capitalization
            Public Property Scope As IdentifierScope
            Public Property DeclaringFile As String        ' full Path to declaring file
            Public Property DeclaringLine As Integer
            Public Property DeclaringColumn As Integer
            Public Property ParentScope As String          ' Parent class/namespace
            Public Property ReturnType As String           ' for methods/properties
            Public Property Parameters As List(Of String)  ' for methods
            Public Property IsPublic As Boolean = True
            Public Property IsStatic As Boolean = False
            Public Property References As List(Of ReferenceInfo)
            Public Property LastModified As DateTime
            
            Public Sub New()
                References = New List(Of ReferenceInfo)()
                Parameters = New List(Of String)()
                LastModified = DateTime.Now
            End Sub
            
            Public ReadOnly Property FullyQualifiedName As String
                Get
                    If String.IsNullOrEmpty(ParentScope) Then
                        Return CanonicalCase
                    Else
                        Return $"{ParentScope}.{CanonicalCase}"
                    End If
                End Get
            End Property
            
            Public ReadOnly Property ReferenceCount As Integer
                Get
                    Return References.Count
                End Get
            End Property
        End Class
        
        ''' <summary>
        ''' Information about a single reference to an identifier
        ''' </summary>
        Public Class ReferenceInfo
            Public Property IdentifierName As String       ' As it appears in code
            Public Property FilePath As String             ' File containing Reference
            Public Property DocumentModel As DocumentModel ' document if loaded
            Public Property Line As Integer
            Public Property Column As Integer
            Public Property Kind As ReferenceKind
            Public Property Context As String              ' Surrounding code snippet
            Public Property IsInconsistent As Boolean      ' Case doesn't match canonical
            
            Public ReadOnly Property Location As String
                Get
                    Dim lFileName As String = System.IO.Path.GetFileName(FilePath)
                    Return $"{lFileName}:{Line}:{Column}"
                End Get
            End Property
        End Class
        
        ''' <summary>
        ''' Represents an inconsistency in identifier casing
        ''' </summary>
        Public Class InconsistencyInfo
            Public Property Declaration As DeclarationInfo
            Public Property Reference As ReferenceInfo
            Public Property CorrectCase As String
            Public Property ActualCase As String
            Public Property Message As String
            
            Public ReadOnly Property Description As String
                Get
                    Return $"'{ActualCase}' should be '{CorrectCase}' at {Reference.Location}"
                End Get
            End Property
        End Class
        
        ' ===== Private Fields =====
        
        ' Main registry - lowercase key to DeclarationInfo
        Private pDeclarations As Dictionary(Of String, DeclarationInfo)
        
        ' Quick lookup by file
        Private pFileIndex As Dictionary(Of String, List(Of DeclarationInfo))
        
        ' Quick lookup by scope
        Private pScopeIndex As Dictionary(Of IdentifierScope, List(Of DeclarationInfo))
        
        ' Inconsistency cache
        Private pInconsistencies As List(Of InconsistencyInfo)
        Private pInconsistenciesByFile As Dictionary(Of String, List(Of InconsistencyInfo))

        ' Track if we're currently in a multi-line string across line parsing
        Private pInMultiLineString As Boolean = False
        Private pMultiLineStringsByFile As New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)

        
        ' Statistics
        Private pTotalDeclarations As Integer = 0
        Private pTotalReferences As Integer = 0
        Private pLastFullScan As DateTime = DateTime.MinValue
        
        ' Lock for thread safety
        Private pLock As New Object()
        
        ' ===== Events =====
        
        Public Event DeclarationAdded(vDeclaration As DeclarationInfo)
        Public Event DeclarationUpdated(vOldCase As String, vNewCase As String, vDeclaration As DeclarationInfo)
        Public Event ReferenceAdded(vReference As ReferenceInfo)
        Public Event InconsistencyDetected(vInconsistency As InconsistencyInfo)
        Public Event InconsistenciesResolved(vCount As Integer)
        
        ' ===== Constructor =====
        
        Public Sub New()
            pDeclarations = New Dictionary(Of String, DeclarationInfo)(StringComparer.OrdinalIgnoreCase)
            pFileIndex = New Dictionary(Of String, List(Of DeclarationInfo))(StringComparer.OrdinalIgnoreCase)
            pScopeIndex = New Dictionary(Of IdentifierScope, List(Of DeclarationInfo))()
            pInconsistencies = New List(Of InconsistencyInfo)()
            pInconsistenciesByFile = New Dictionary(Of String, List(Of InconsistencyInfo))(StringComparer.OrdinalIgnoreCase)
            
            ' Initialize scope index
            For Each lScope As IdentifierScope In [Enum].GetValues(GetType(IdentifierScope))
                pScopeIndex(lScope) = New List(Of DeclarationInfo)()
            Next
        End Sub
        
        ' ===== Public Methods - Declaration Management =====
        
        ''' <summary>
        ''' Register a new declaration or update existing
        ''' </summary>
        Public Function RegisterDeclaration(vIdentifier As String, vScope As IdentifierScope, 
                                           vFile As String, vLine As Integer, vColumn As Integer,
                                           Optional vParentScope As String = "") As DeclarationInfo
            SyncLock pLock
                Try
                    Dim lKey As String = vIdentifier.ToLowerInvariant()
                    Dim lDeclaration As DeclarationInfo = Nothing
                    
                    ' Check if declaration exists
                    If pDeclarations.TryGetValue(lKey, lDeclaration) Then
                        ' Update existing declaration
                        Dim lOldCase As String = lDeclaration.CanonicalCase
                        lDeclaration.CanonicalCase = vIdentifier
                        lDeclaration.DeclaringFile = vFile
                        lDeclaration.DeclaringLine = vLine
                        lDeclaration.DeclaringColumn = vColumn
                        lDeclaration.ParentScope = vParentScope
                        lDeclaration.LastModified = DateTime.Now
                        
                        ' Check all references for inconsistencies
                        If Not lOldCase.Equals(vIdentifier, StringComparison.Ordinal) Then
                            UpdateReferenceConsistency(lDeclaration)
                            RaiseEvent DeclarationUpdated(lOldCase, vIdentifier, lDeclaration)
                        End If
                    Else
                        ' Create new declaration
                        lDeclaration = New DeclarationInfo() With {
                            .Identifier = vIdentifier,
                            .CanonicalCase = vIdentifier,
                            .Scope = vScope,
                            .DeclaringFile = vFile,
                            .DeclaringLine = vLine,
                            .DeclaringColumn = vColumn,
                            .ParentScope = vParentScope
                        }
                        
                        ' Add to main registry
                        pDeclarations(lKey) = lDeclaration
                        pTotalDeclarations += 1
                        
                        ' Add to file index
                        If Not pFileIndex.ContainsKey(vFile) Then
                            pFileIndex(vFile) = New List(Of DeclarationInfo)()
                        End If
                        pFileIndex(vFile).Add(lDeclaration)
                        
                        ' Add to scope index
                        pScopeIndex(vScope).Add(lDeclaration)
                        
                        RaiseEvent DeclarationAdded(lDeclaration)
                    End If
                    
                    Return lDeclaration
                    
                Catch ex As Exception
                    Console.WriteLine($"RegisterDeclaration error: {ex.Message}")
                    Return Nothing
                End Try
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Register a reference to an identifier
        ''' </summary>
        Public Function RegisterReference(vIdentifier As String, vFile As String, 
                                         vLine As Integer, vColumn As Integer,
                                         vKind As ReferenceKind,
                                         Optional vDocumentModel As DocumentModel = Nothing) As ReferenceInfo
            SyncLock pLock
                Try
                    Dim lKey As String = vIdentifier.ToLowerInvariant()
                    Dim lDeclaration As DeclarationInfo = Nothing
                    
                    ' Find the declaration
                    If Not pDeclarations.TryGetValue(lKey, lDeclaration) Then
                        ' No declaration found - might be external or not yet parsed
                        Return Nothing
                    End If
                    
                    ' Create reference info
                    Dim lReference As New ReferenceInfo() With {
                        .IdentifierName = vIdentifier,
                        .FilePath = vFile,
                        .DocumentModel = vDocumentModel,
                        .Line = vLine,
                        .Column = vColumn,
                        .Kind = vKind,
                        .IsInconsistent = Not vIdentifier.Equals(lDeclaration.CanonicalCase, StringComparison.Ordinal)
                    }
                    
                    ' Add to declaration's references
                    lDeclaration.References.Add(lReference)
                    pTotalReferences += 1
                    
                    ' Check for inconsistency
                    If lReference.IsInconsistent Then
                        Dim lInconsistency As New InconsistencyInfo() With {
                            .Declaration = lDeclaration,
                            .Reference = lReference,
                            .CorrectCase = lDeclaration.CanonicalCase,
                            .ActualCase = vIdentifier
                        }
                        
                        pInconsistencies.Add(lInconsistency)
                        
                        If Not pInconsistenciesByFile.ContainsKey(vFile) Then
                            pInconsistenciesByFile(vFile) = New List(Of InconsistencyInfo)()
                        End If
                        pInconsistenciesByFile(vFile).Add(lInconsistency)
                        
                        RaiseEvent InconsistencyDetected(lInconsistency)
                    End If
                    
                    RaiseEvent ReferenceAdded(lReference)
                    Return lReference
                    
                Catch ex As Exception
                    Console.WriteLine($"RegisterReference error: {ex.Message}")
                    Return Nothing
                End Try
            End SyncLock
        End Function
        
        ' ===== Public Methods - Querying =====
        
        ''' <summary>
        ''' Get declaration for an identifier
        ''' </summary>
        Public Function GetDeclaration(vIdentifier As String) As DeclarationInfo
            SyncLock pLock
                Try
                    Dim lKey As String = vIdentifier.ToLowerInvariant()
                    Dim lDeclaration As DeclarationInfo = Nothing
                    pDeclarations.TryGetValue(lKey, lDeclaration)
                    Return lDeclaration
                    
                Catch ex As Exception
                    Console.WriteLine($"GetDeclaration error: {ex.Message}")
                    Return Nothing
                End Try
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Get all references for an identifier
        ''' </summary>
        Public Function GetReferences(vIdentifier As String) As List(Of ReferenceInfo)
            SyncLock pLock
                Try
                    Dim lDeclaration As DeclarationInfo = GetDeclaration(vIdentifier)
                    If lDeclaration IsNot Nothing Then
                        Return New List(Of ReferenceInfo)(lDeclaration.References)
                    End If
                    Return New List(Of ReferenceInfo)()
                    
                Catch ex As Exception
                    Console.WriteLine($"GetReferences error: {ex.Message}")
                    Return New List(Of ReferenceInfo)()
                End Try
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Get all declarations in a file
        ''' </summary>
        Public Function GetDeclarationsInFile(vFilePath As String) As List(Of DeclarationInfo)
            SyncLock pLock
                Try
                    Dim lDeclarations As List(Of DeclarationInfo) = Nothing
                    If pFileIndex.TryGetValue(vFilePath, lDeclarations) Then
                        Return New List(Of DeclarationInfo)(lDeclarations)
                    End If
                    Return New List(Of DeclarationInfo)()
                    
                Catch ex As Exception
                    Console.WriteLine($"GetDeclarationsInFile error: {ex.Message}")
                    Return New List(Of DeclarationInfo)()
                End Try
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Get all declarations
        ''' </summary>
        Public Function GetAllDeclarations() As List(Of DeclarationInfo)
            SyncLock pLock
                Return New List(Of DeclarationInfo)(pDeclarations.Values)
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Get all inconsistencies project-wide
        ''' </summary>
        Public Function GetAllInconsistencies() As List(Of InconsistencyInfo)
            SyncLock pLock
                Return New List(Of InconsistencyInfo)(pInconsistencies)
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Get inconsistencies in a specific file
        ''' </summary>
        Public Function GetInconsistenciesInFile(vFilePath As String) As List(Of InconsistencyInfo)
            SyncLock pLock
                Try
                    Dim lInconsistencies As List(Of InconsistencyInfo) = Nothing
                    If pInconsistenciesByFile.TryGetValue(vFilePath, lInconsistencies) Then
                        Return New List(Of InconsistencyInfo)(lInconsistencies)
                    End If
                    Return New List(Of InconsistencyInfo)()
                    
                Catch ex As Exception
                    Console.WriteLine($"GetInconsistenciesInFile error: {ex.Message}")
                    Return New List(Of InconsistencyInfo)()
                End Try
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Get inconsistencies in a specific scope
        ''' </summary>
        Public Function GetInconsistenciesInScope(vScope As IdentifierScope) As List(Of InconsistencyInfo)
            SyncLock pLock
                Try
                    Dim lResult As New List(Of InconsistencyInfo)()
                    
                    For Each lInc In pInconsistencies
                        If lInc.Declaration.Scope = vScope Then
                            lResult.Add(lInc)
                        End If
                    Next
                    
                    Return lResult
                    
                Catch ex As Exception
                    Console.WriteLine($"GetInconsistenciesInScope error: {ex.Message}")
                    Return New List(Of InconsistencyInfo)()
                End Try
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Get count of inconsistencies per file
        ''' </summary>
        Public Function GetInconsistencyCountByFile() As Dictionary(Of String, Integer)
            SyncLock pLock
                Try
                    Dim lCounts As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
                    
                    For Each kvp In pInconsistenciesByFile
                        lCounts(kvp.key) = kvp.Value.Count
                    Next
                    
                    Return lCounts
                    
                Catch ex As Exception
                    Console.WriteLine($"GetInconsistencyCountByFile error: {ex.Message}")
                    Return New Dictionary(Of String, Integer)()
                End Try
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Get rename preview showing all affected locations
        ''' </summary>
        Public Function GetRenamePreview(vIdentifier As String, vNewName As String) As Dictionary(Of String, List(Of String))
            SyncLock pLock
                Try
                    Dim lPreview As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
                    Dim lDeclaration As DeclarationInfo = GetDeclaration(vIdentifier)
                    
                    If lDeclaration Is Nothing Then Return lPreview
                    
                    ' Group references by file
                    Dim lReferencesByFile As New Dictionary(Of String, List(Of ReferenceInfo))(StringComparer.OrdinalIgnoreCase)
                    
                    For Each lRef In lDeclaration.References
                        If Not lReferencesByFile.ContainsKey(lRef.FilePath) Then
                            lReferencesByFile(lRef.FilePath) = New List(Of ReferenceInfo)()
                        End If
                        lReferencesByFile(lRef.FilePath).Add(lRef)
                    Next
                    
                    ' Build preview for each file
                    For Each kvp In lReferencesByFile
                        Dim lFilePreview As New List(Of String)()
                        
                        For Each lRef In kvp.Value
                            lFilePreview.Add($"Line {lRef.Line}: '{lRef.IdentifierName}' → '{vNewName}'")
                        Next
                        
                        lPreview(kvp.key) = lFilePreview
                    Next
                    
                    Return lPreview
                    
                Catch ex As Exception
                    Console.WriteLine($"GetRenamePreview error: {ex.Message}")
                    Return New Dictionary(Of String, List(Of String))()
                End Try
            End SyncLock
        End Function
        
        ' ===== Public Methods - Bulk Operations =====
        
        ''' <summary>
        ''' Normalize all identifiers in a document model (in memory)
        ''' </summary>
        Public Function NormalizeDocument(vDocument As DocumentModel) As Integer
            SyncLock pLock
                Try
                    Dim lChangesCount As Integer = 0
                    
                    ' Process each line in the document
                    For i As Integer = 0 To vDocument.LineCount - 1
                        Dim lLine As String = vDocument.GetLineText(i)
                        Dim lNormalizedLine As String = NormalizeLine(lLine)
                        
                        If Not lLine.Equals(lNormalizedLine, StringComparison.Ordinal) Then
                            vDocument.UpdateLine(i, lNormalizedLine)
                            lChangesCount += 1
                        End If
                    Next
                    
                    ' Remove inconsistencies for this file
                    If pInconsistenciesByFile.ContainsKey(vDocument.FilePath) Then
                        Dim lFileInconsistencies As List(Of InconsistencyInfo) = pInconsistenciesByFile(vDocument.FilePath)
                        
                        ' Remove from main list
                        For Each lInconsistency In lFileInconsistencies
                            pInconsistencies.Remove(lInconsistency)
                        Next
                        
                        ' Clear file list
                        pInconsistenciesByFile.Remove(vDocument.FilePath)
                        
                        RaiseEvent InconsistenciesResolved(lFileInconsistencies.Count)
                    End If
                    
                    Return lChangesCount
                    
                Catch ex As Exception
                    Console.WriteLine($"NormalizeDocument error: {ex.Message}")
                    Return 0
                End Try
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Normalize entire project (all registered files)
        ''' </summary>
        Public Function NormalizeEntireProject(vProjectManager As ProjectManager) As Integer
            SyncLock pLock
                Try
                    Dim lTotalChanges As Integer = 0
                    Dim lProcessedFiles As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                    
                    ' Get all project files
                    Dim lProjectInfo As ProjectInfo = vProjectManager.GetProjectInfo(vProjectManager.CurrentProjectPath)
                    If lProjectInfo Is Nothing Then Return 0
                    
                    For Each lFile In lProjectInfo.SourceFiles
                        Dim lFullPath As String = System.IO.Path.Combine(vProjectManager.CurrentProjectDirectory, lFile)
                        
                        If Not lProcessedFiles.Contains(lFullPath) Then
                            lProcessedFiles.Add(lFullPath)
                            
                            ' Read file
                            Dim lContent As String = System.IO.File.ReadAllText(lFullPath)
                            Dim lLines() As String = lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                            
                            ' Normalize each line
                            Dim lHasChanges As Boolean = False
                            For i As Integer = 0 To lLines.Length - 1
                                Dim lNormalizedLine As String = NormalizeLine(lLines(i))
                                If Not lLines(i).Equals(lNormalizedLine, StringComparison.Ordinal) Then
                                    lLines(i) = lNormalizedLine
                                    lHasChanges = True
                                End If
                            Next
                            
                            ' Write back if changed
                            If lHasChanges Then
                                System.IO.File.WriteAllText(lFullPath, String.Join(Environment.NewLine, lLines))
                                lTotalChanges += 1
                            End If
                        End If
                    Next
                    
                    ' Clear all inconsistencies
                    Dim lInconsistencyCount As Integer = pInconsistencies.Count
                    pInconsistencies.Clear()
                    pInconsistenciesByFile.Clear()
                    
                    If lInconsistencyCount > 0 Then
                        RaiseEvent InconsistenciesResolved(lInconsistencyCount)
                    End If
                    
                    Return lTotalChanges
                    
                Catch ex As Exception
                    Console.WriteLine($"NormalizeEntireProject error: {ex.Message}")
                    Return 0
                End Try
            End SyncLock
        End Function
        
        ''' <summary>
        ''' Rename an identifier across all references
        ''' </summary>
        Public Function RenameIdentifier(vOldName As String, vNewName As String) As List(Of ReferenceInfo)
            SyncLock pLock
                Try
                    Dim lDeclaration As DeclarationInfo = GetDeclaration(vOldName)
                    If lDeclaration Is Nothing Then Return New List(Of ReferenceInfo)()
                    
                    ' Update declaration
                    Dim lOldKey As String = vOldName.ToLowerInvariant()
                    Dim lNewKey As String = vNewName.ToLowerInvariant()
                    
                    ' Remove old key
                    pDeclarations.Remove(lOldKey)
                    
                    ' Update declaration
                    lDeclaration.Identifier = vNewName
                    lDeclaration.CanonicalCase = vNewName
                    lDeclaration.LastModified = DateTime.Now
                    
                    ' Add with new key
                    pDeclarations(lNewKey) = lDeclaration
                    
                    ' Clear inconsistencies as all references need updating
                    For Each lRef In lDeclaration.References
                        lRef.IsInconsistent = True ' Mark all as needing update
                    Next
                    
                    RaiseEvent DeclarationUpdated(vOldName, vNewName, lDeclaration)
                    
                    Return lDeclaration.References
                    
                Catch ex As Exception
                    Console.WriteLine($"RenameIdentifier error: {ex.Message}")
                    Return New List(Of ReferenceInfo)()
                End Try
            End SyncLock
        End Function
        
        ' ===== Public Methods - File Operations =====
        
'        Public Sub IndexFile(vFilePath As String, Optional vDocumentModel As DocumentModel = Nothing)
'            Try
'                Dim lContent As String
'                Dim lLines() As String
'                
'                If vDocumentModel IsNot Nothing Then
'                    lContent = vDocumentModel.GetAllText()
'                Else
'                    lContent = System.IO.File.ReadAllText(vFilePath)
'                End If
'                
'                lLines = lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
'                
'                ' Parse declarations and references
'                for i As Integer = 0 To lLines.Length - 1
'                    ParseLineForDeclarations(vFilePath, lLines(i), i)
'                    ParseLineForReferences(vFilePath, lLines(i), i)
'                Next
'                
'            Catch ex As Exception
'                Console.WriteLine($"IndexFile error: {ex.Message}")
'            End Try
'        End Sub
        
        ''' <summary>
        ''' Remove all declarations and references for a file
        ''' </summary>
        Public Sub RemoveFile(vFilePath As String)
            SyncLock pLock
                Try
                    ' Remove declarations
                    If pFileIndex.ContainsKey(vFilePath) Then
                        Dim lDeclarations As List(Of DeclarationInfo) = pFileIndex(vFilePath)
                        
                        For Each lDecl In lDeclarations
                            Dim lKey As String = lDecl.Identifier.ToLowerInvariant()
                            pDeclarations.Remove(lKey)
                            pScopeIndex(lDecl.Scope).Remove(lDecl)
                            pTotalDeclarations -= 1
                        Next
                        
                        pFileIndex.Remove(vFilePath)
                    End If
                    
                    ' Remove references from all declarations
                    For Each lDecl In pDeclarations.Values
                        lDecl.References.RemoveAll(Function(r) r.FilePath.Equals(vFilePath, StringComparison.OrdinalIgnoreCase))
                    Next
                    
                    ' Remove inconsistencies
                    If pInconsistenciesByFile.ContainsKey(vFilePath) Then
                        Dim lFileInconsistencies As List(Of InconsistencyInfo) = pInconsistenciesByFile(vFilePath)
                        
                        For Each lInconsistency In lFileInconsistencies
                            pInconsistencies.Remove(lInconsistency)
                        Next
                        
                        pInconsistenciesByFile.Remove(vFilePath)
                    End If
                    
                Catch ex As Exception
                    Console.WriteLine($"RemoveFile error: {ex.Message}")
                End Try
            End SyncLock
        End Sub
        
        ''' <summary>
        ''' Update line in document when identifier is edited
        ''' </summary>
        Public Sub UpdateLineAfterEdit(vFilePath As String, vLineNumber As Integer, vNewLineText As String, 
                                       Optional vDocumentModel As DocumentModel = Nothing)
            Try
                ' Re-parse the line for declarations and references
                ParseLineForDeclarations(vFilePath, vNewLineText, vLineNumber, vDocumentModel)
                ParseLineForReferences(vFilePath, vNewLineText, vLineNumber)
                
                ' Check for inconsistencies
                RebuildInconsistenciesForFile(vFilePath)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineAfterEdit error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Total number of declarations in the registry
        ''' </summary>
        Public ReadOnly Property TotalDeclarations As Integer
            Get
                SyncLock pLock
                    Return pTotalDeclarations
                End SyncLock
            End Get
        End Property
        
        ''' <summary>
        ''' Total number of references in the registry
        ''' </summary>
        Public ReadOnly Property TotalReferences As Integer
            Get
                SyncLock pLock
                    Return pTotalReferences
                End SyncLock
            End Get
        End Property
        
        ''' <summary>
        ''' Total number of inconsistencies in the registry
        ''' </summary>
        Public ReadOnly Property TotalInconsistencies As Integer
            Get
                SyncLock pLock
                    Return pInconsistencies.Count
                End SyncLock
            End Get
        End Property
        
        ' ===== Missing Private Method =====
        
        ''' <summary>
        ''' Rebuild inconsistencies for a specific file
        ''' </summary>
        Private Sub RebuildInconsistenciesForFile(vFilePath As String)
            Try
                ' Remove existing inconsistencies for this file
                If pInconsistenciesByFile.ContainsKey(vFilePath) Then
                    Dim lOldInconsistencies As List(Of InconsistencyInfo) = pInconsistenciesByFile(vFilePath)
                    
                    ' Remove from main list
                    For Each lInconsistency In lOldInconsistencies
                        pInconsistencies.Remove(lInconsistency)
                    Next
                    
                    ' Clear file list
                    pInconsistenciesByFile.Remove(vFilePath)
                End If
                
                ' Get all declarations in this file
                Dim lDeclarations As List(Of DeclarationInfo) = GetDeclarationsInFile(vFilePath)
                
                ' Check all references in the file for inconsistencies
                For Each lDeclaration In lDeclarations
                    ' Check each reference for this declaration
                    For Each lReference In lDeclaration.References
                        If lReference.FilePath.Equals(vFilePath, StringComparison.OrdinalIgnoreCase) Then
                            ' Check if reference case matches declaration
                            lReference.IsInconsistent = Not lReference.IdentifierName.Equals(lDeclaration.CanonicalCase, StringComparison.Ordinal)
                            
                            If lReference.IsInconsistent Then
                                ' Create new inconsistency
                                Dim lInconsistency As New InconsistencyInfo With {
                                    .Declaration = lDeclaration,
                                    .Reference = lReference,
                                    .CorrectCase = lDeclaration.CanonicalCase,
                                    .ActualCase = lReference.IdentifierName,
                                    .Message = $"'{lReference.IdentifierName}' should be '{lDeclaration.CanonicalCase}' at {System.IO.Path.GetFileName(lReference.FilePath)}:{lReference.Line + 1}:{lReference.Column + 1}"
                                }
                                
                                ' Add to lists
                                pInconsistencies.Add(lInconsistency)
                                
                                If Not pInconsistenciesByFile.ContainsKey(vFilePath) Then
                                    pInconsistenciesByFile(vFilePath) = New List(Of InconsistencyInfo)()
                                End If
                                pInconsistenciesByFile(vFilePath).Add(lInconsistency)
                                
                                ' Raise event
                                RaiseEvent InconsistencyDetected(lInconsistency)
                            End If
                        End If
                    Next
                Next
                
                ' Also check references TO declarations in this file from other files
                For Each lDeclaration In pDeclarations.Values
                    If lDeclaration.DeclaringFile.Equals(vFilePath, StringComparison.OrdinalIgnoreCase) Then
                        ' Update consistency for all references to this declaration
                        UpdateReferenceConsistency(lDeclaration)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"RebuildInconsistenciesForFile error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
