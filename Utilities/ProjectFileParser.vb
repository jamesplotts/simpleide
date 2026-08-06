' Utilities/ProjectFileParser.vb - VB.NET project file parsing utilities with root namespace support
Imports System.IO
Imports System.Xml
Imports System.Collections.Generic

Namespace Utilities

    Public Class ProjectFileParser
        
        Public Class ProjectInfo
            Public Property ProjectName As String
            Public Property ProjectPath As String
            Public Property ProjectDirectory As String
            Public Property RootNamespace As String  ' ADD THIS PROPERTY
            Public Property CompileItems As New List(Of String)
            Public Property References As New List(Of ReferenceInfo)
            Public Property PackageReferences As New List(Of PackageInfo)

            ''' <summary>
            ''' Project-level VB.NET namespace imports (&lt;Import Include="System" /&gt; items) -
            ''' auto-imported into every file in the project, distinct from a file's own
            ''' in-source Imports statements
            ''' </summary>
            Public Property ProjectImports As New List(Of String)
        End Class
        
        Public Class ReferenceInfo
            Public Property Name As String
            Public Property Version As String
            Public Property HintPath As String
        End Class
        
        Public Class PackageInfo
            Public Property Name As String
            Public Property Version As String
        End Class
        
        Public Shared Function ParseProjectFile(vProjectFilePath As String) As ProjectInfo
            Dim lInfo As New ProjectInfo()
            lInfo.ProjectPath = vProjectFilePath
            lInfo.ProjectDirectory = System.IO.Path.GetDirectoryName(vProjectFilePath)
            lInfo.ProjectName = System.IO.Path.GetFileNameWithoutExtension(vProjectFilePath)
            
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(vProjectFilePath)
                
                ' Create namespace manager for MSBuild namespace
                Dim lNamespaceManager As New XmlNamespaceManager(lDoc.NameTable)
                lNamespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                ' Parse root namespace from PropertyGroup
                ParseRootNamespace(lDoc, lNamespaceManager, lInfo)
                
                ' Parse compile items
                ParseCompileItems(lDoc, lNamespaceManager, lInfo)
                
                ' Parse references
                ParseReferences(lDoc, lNamespaceManager, lInfo)
                
                ' Parse package references
                ParsePackageReferences(lDoc, lInfo)

                ' Parse project-level namespace imports
                ParseProjectImports(lDoc, lNamespaceManager, lInfo)
                
                ' If no root namespace was found, use project name as fallback
                If String.IsNullOrEmpty(lInfo.RootNamespace) Then
                    lInfo.RootNamespace = lInfo.ProjectName
                End If
                
            Catch ex As Exception
                Console.WriteLine($"Error parsing project file: {ex.Message}")
                ' Fallback to project name if parsing fails
                If String.IsNullOrEmpty(lInfo.RootNamespace) Then
                    lInfo.RootNamespace = lInfo.ProjectName
                End If
            End Try
            
            Return lInfo
        End Function
        
        Private Shared Sub ParseRootNamespace(vDoc As XmlDocument, vNamespaceManager As XmlNamespaceManager, vInfo As ProjectInfo)
            Try
                ' Try with namespace first (older project format)
                Dim lRootNamespaceNode As XmlNode = vDoc.SelectSingleNode("//ms:RootNamespace", vNamespaceManager)
                
                ' Try without namespace (newer SDK-style projects)
                If lRootNamespaceNode Is Nothing Then
                    lRootNamespaceNode = vDoc.SelectSingleNode("//RootNamespace")
                End If
                
                If lRootNamespaceNode IsNot Nothing Then
                    vInfo.RootNamespace = lRootNamespaceNode.InnerText.Trim()
                    Console.WriteLine($"Found root namespace: {vInfo.RootNamespace}")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"ParseRootNamespace error: {ex.Message}")
            End Try
        End Sub
        
        Private Shared Sub ParseCompileItems(vDoc As XmlDocument, vNamespaceManager As XmlNamespaceManager, vInfo As ProjectInfo)
            ' Try both with and without namespace (for different project file formats)
            Dim lCompileNodes As XmlNodeList = vDoc.SelectNodes("//ms:Compile[@Include]", vNamespaceManager)
            If lCompileNodes.Count = 0 Then
                ' Try without namespace for newer format
                lCompileNodes = vDoc.SelectNodes("//Compile[@Include]")
            End If
            
            for each lNode As XmlNode in lCompileNodes
                Dim lInclude As String = lNode.Attributes("Include").Value
                vInfo.CompileItems.Add(lInclude)
            Next
        End Sub
        
        Private Shared Sub ParseReferences(vDoc As XmlDocument, vNamespaceManager As XmlNamespaceManager, vInfo As ProjectInfo)
            Dim lReferenceNodes As XmlNodeList = vDoc.SelectNodes("//ms:Reference[@Include]", vNamespaceManager)
            If lReferenceNodes.Count = 0 Then
                lReferenceNodes = vDoc.SelectNodes("//Reference[@Include]")
            End If
            
            for each lNode As XmlNode in lReferenceNodes
                Dim lRef As New ReferenceInfo()
                Dim lIncludeValue As String = lNode.Attributes("Include").Value
                
                ' Parse the reference name and version
                Dim lParts() As String = lIncludeValue.Split(","c)
                lRef.Name = lParts(0).Trim()
                
                ' Extract version if present
                for i As Integer = 1 To lParts.Length - 1
                    If lParts(i).Trim().StartsWith("Version=") Then
                        lRef.Version = lParts(i).Trim().Substring(8)
                    End If
                Next
                
                ' Check for HintPath
                Dim lHintPathNode As XmlNode = lNode.SelectSingleNode("ms:HintPath", vNamespaceManager)
                If lHintPathNode Is Nothing Then
                    lHintPathNode = lNode.SelectSingleNode("HintPath")
                End If
                If lHintPathNode IsNot Nothing Then
                    lRef.HintPath = lHintPathNode.InnerText
                End If
                
                vInfo.References.Add(lRef)
            Next
        End Sub
        
        Private Shared Sub ParsePackageReferences(vDoc As XmlDocument, vInfo As ProjectInfo)
            ' Package references (for newer SDK-style projects)
            Dim lPackageNodes As XmlNodeList = vDoc.SelectNodes("//PackageReference[@Include]")
            
            for each lNode As XmlNode in lPackageNodes
                Dim lPackage As New PackageInfo()
                lPackage.Name = lNode.Attributes("Include").Value
                
                If lNode.Attributes("Version") IsNot Nothing Then
                    lPackage.Version = lNode.Attributes("Version").Value
                End If
                
                vInfo.PackageReferences.Add(lPackage)
            Next
        End Sub
        
        ''' <summary>
        ''' Parses project-level namespace imports (&lt;Import Include="System" /&gt; items,
        ''' the VB.NET-specific "Imports" ItemGroup entries VS's project properties UI writes
        ''' as global imports) - distinct from MSBuild's own &lt;Import Project="..."&gt;
        ''' element (which imports a .props/.targets file, an unrelated concept sharing the
        ''' same element name)
        ''' </summary>
        Private Shared Sub ParseProjectImports(vDoc As XmlDocument, vNamespaceManager As XmlNamespaceManager, vInfo As ProjectInfo)
            Try
                Dim lImportNodes As XmlNodeList = vDoc.SelectNodes("//ms:Import[@Include]", vNamespaceManager)
                If lImportNodes.Count = 0 Then
                    lImportNodes = vDoc.SelectNodes("//Import[@Include]")
                End If

                for each lNode As XmlNode in lImportNodes
                    Dim lNamespaceName As String = lNode.Attributes("Include").Value
                    If Not String.IsNullOrWhiteSpace(lNamespaceName) Then
                        vInfo.ProjectImports.Add(lNamespaceName.Trim())
                    End If
                Next
            Catch ex As Exception
                Console.WriteLine($"ParseProjectImports error: {ex.Message}")
            End Try
        End Sub

        Public Shared Function GetProjectFileExtensions() As String()
            Return {".vbproj", ".csproj", ".fsproj"}
        End Function
        
        Public Shared Function IsProjectFile(vFilePath As String) As Boolean
            Dim lExtension As String = System.IO.Path.GetExtension(vFilePath).ToLower()
            Return GetProjectFileExtensions().Contains(lExtension)
        End Function
    End Class
End Namespace
