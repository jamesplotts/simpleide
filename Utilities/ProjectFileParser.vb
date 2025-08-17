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
            
            For Each lNode As XmlNode In lCompileNodes
                Dim lInclude As String = lNode.Attributes("Include").Value
                vInfo.CompileItems.Add(lInclude)
            Next
        End Sub
        
        Private Shared Sub ParseReferences(vDoc As XmlDocument, vNamespaceManager As XmlNamespaceManager, vInfo As ProjectInfo)
            Dim lReferenceNodes As XmlNodeList = vDoc.SelectNodes("//ms:Reference[@Include]", vNamespaceManager)
            If lReferenceNodes.Count = 0 Then
                lReferenceNodes = vDoc.SelectNodes("//Reference[@Include]")
            End If
            
            For Each lNode As XmlNode In lReferenceNodes
                Dim lRef As New ReferenceInfo()
                Dim lIncludeValue As String = lNode.Attributes("Include").Value
                
                ' Parse the reference name and version
                Dim lParts() As String = lIncludeValue.Split(","c)
                lRef.Name = lParts(0).Trim()
                
                ' Extract version if present
                For i As Integer = 1 To lParts.Length - 1
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
            
            For Each lNode As XmlNode In lPackageNodes
                Dim lPackage As New PackageInfo()
                lPackage.Name = lNode.Attributes("Include").Value
                
                If lNode.Attributes("Version") IsNot Nothing Then
                    lPackage.Version = lNode.Attributes("Version").Value
                End If
                
                vInfo.PackageReferences.Add(lPackage)
            Next
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
