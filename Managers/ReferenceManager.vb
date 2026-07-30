 
' ReferenceManager.vb - Core reference management logic for SimpleIDE
Imports System.IO
Imports System.Xml
Imports System.Collections.Generic
Imports System.Linq
Imports SimpleIDE.Models

Namespace Managers

    Public Class ReferenceManager
        
        ' Reference type enumeration
        Public Enum ReferenceType
            eUnspecified
            eAssembly
            ePackage
            eProject
            eLastValue
        End Enum
        
        ' Reference info class
        Public Class ReferenceInfo
            Public Property Name As String
            Public Property Type As ReferenceType
            Public Property Version As String
            Public Property Path As String
            Public Property Include As String
            Public Property TargetFramework As String
            Public Property ShouldLoad As Boolean = True  
            Public Property OutputType As String
            Public Property RootNamespace As String
            Public Property IsResolved As Boolean = True
            Public Property Description As String
        End Class
        
        ' Validation result class
        Public Class ValidationResult
            Public Property IsValid As Boolean
            Public Property ErrorMessage As String
        End Class
        
        ' Add assembly reference
        Public Function AddAssemblyReference(vProjectFile As String, vAssemblyName As String, vHintPath As String) As Boolean
            Try
                Dim lDoc As New XmlDocument()
                lDoc.PreserveWhitespace = True
                lDoc.Load(vProjectFile)
                
                ' Create namespace manager
                Dim lNsMgr As New XmlNamespaceManager(lDoc.NameTable)
                lNsMgr.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                ' Check if reference already exists
                Dim lExisting = FindExistingReference(lDoc, lNsMgr, vAssemblyName, ReferenceType.eAssembly)
                If lExisting IsNot Nothing Then
                    Console.WriteLine($"Assembly Reference '{vAssemblyName}' already exists")
                    Return False
                End If
                
                ' Find or create ItemGroup for references
                Dim lItemGroup As XmlNode = FindOrCreateItemGroup(lDoc, lNsMgr, "Reference")
                
                ' Create Reference element
                Dim lReference As XmlElement = lDoc.CreateElement("Reference", lDoc.DocumentElement.NamespaceURI)
                lReference.SetAttribute("Include", vAssemblyName)
                
                ' Add HintPath if provided
                If Not String.IsNullOrEmpty(vHintPath) Then
                    Dim lHintPath As XmlElement = lDoc.CreateElement("HintPath", lDoc.DocumentElement.NamespaceURI)
                    lHintPath.InnerText = vHintPath
                    lReference.AppendChild(lHintPath)
                End If
                
                lItemGroup.AppendChild(lReference)
                
                ' Save with formatting
                SaveProjectFile(lDoc, vProjectFile)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error adding assembly Reference: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Add package reference
        Public Function AddPackageReference(vProjectFile As String, vPackageName As String, vVersion As String) As Boolean
            Try
                Dim lDoc As New XmlDocument()
                lDoc.PreserveWhitespace = True
                lDoc.Load(vProjectFile)
                
                ' Create namespace manager
                Dim lNsMgr As New XmlNamespaceManager(lDoc.NameTable)
                lNsMgr.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                ' Check if package already exists
                Dim lExisting = FindExistingReference(lDoc, lNsMgr, vPackageName, ReferenceType.ePackage)
                If lExisting IsNot Nothing Then
                    Console.WriteLine($"Package Reference '{vPackageName}' already exists")
                    Return False
                End If
                
                ' Find or create ItemGroup for package references
                Dim lItemGroup As XmlNode = FindOrCreateItemGroup(lDoc, lNsMgr, "PackageReference")
                
                ' Create PackageReference element
                Dim lPackageRef As XmlElement = lDoc.CreateElement("PackageReference", lDoc.DocumentElement.NamespaceURI)
                lPackageRef.SetAttribute("Include", vPackageName)
                lPackageRef.SetAttribute("Version", vVersion)
                
                lItemGroup.AppendChild(lPackageRef)
                
                ' Save with formatting
                SaveProjectFile(lDoc, vProjectFile)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error adding Package Reference: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Add project reference
        Public Function AddProjectReference(vProjectFile As String, vReferencePath As String) As Boolean
            Try
                Dim lDoc As New XmlDocument()
                lDoc.PreserveWhitespace = True
                lDoc.Load(vProjectFile)
                
                ' Make path relative to project file
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(vProjectFile)
                Dim lRelativePath As String = GetRelativePath(lProjectDir, vReferencePath)
                
                ' Create namespace manager
                Dim lNsMgr As New XmlNamespaceManager(lDoc.NameTable)
                lNsMgr.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                ' Check if project reference already exists
                Dim lExisting = FindExistingReference(lDoc, lNsMgr, lRelativePath, ReferenceType.eProject)
                If lExisting IsNot Nothing Then
                    Console.WriteLine($"project Reference '{lRelativePath}' already exists")
                    Return False
                End If
                
                ' Find or create ItemGroup for project references
                Dim lItemGroup As XmlNode = FindOrCreateItemGroup(lDoc, lNsMgr, "ProjectReference")
                
                ' Create ProjectReference element
                Dim lProjectRef As XmlElement = lDoc.CreateElement("ProjectReference", lDoc.DocumentElement.NamespaceURI)
                lProjectRef.SetAttribute("Include", lRelativePath)
                
                lItemGroup.AppendChild(lProjectRef)
                
                ' Save with formatting
                SaveProjectFile(lDoc, vProjectFile)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"error adding project Reference: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Remove reference
        Public Function RemoveReference(vProjectFile As String, vReferenceName As String, vReferenceType As ReferenceType) As Boolean
            Try
                Dim lDoc As New XmlDocument()
                lDoc.PreserveWhitespace = True
                lDoc.Load(vProjectFile)
                
                Dim lNsMgr As New XmlNamespaceManager(lDoc.NameTable)
                lNsMgr.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                Dim lNodeName As String = GetNodeNameForType(vReferenceType)
                Dim lNode As XmlNode = FindExistingReference(lDoc, lNsMgr, vReferenceName, vReferenceType)
                
                If lNode IsNot Nothing Then
                    ' Remove the node
                    Dim lParent As XmlNode = lNode.ParentNode
                    lParent.RemoveChild(lNode)
                    
                    ' If ItemGroup is empty, remove it too
                    If lParent.ChildNodes.Count = 0 Then
                        lParent.ParentNode.RemoveChild(lParent)
                    End If
                    
                    ' Save with formatting
                    SaveProjectFile(lDoc, vProjectFile)
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"error removing Reference: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Update package reference version
        Public Function UpdatePackageReference(vProjectFile As String, vPackageName As String, vNewVersion As String) As Boolean
            Try
                Dim lDoc As New XmlDocument()
                lDoc.PreserveWhitespace = True
                lDoc.Load(vProjectFile)
                
                Dim lNsMgr As New XmlNamespaceManager(lDoc.NameTable)
                lNsMgr.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                Dim lNode As XmlNode = FindExistingReference(lDoc, lNsMgr, vPackageName, ReferenceType.ePackage)
                
                If lNode IsNot Nothing Then
                    CType(lNode, XmlElement).SetAttribute("Version", vNewVersion)
                    
                    ' Save with formatting
                    SaveProjectFile(lDoc, vProjectFile)
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"error updating Package Reference: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' Get all references from project
        Public Function GetAllReferences(vProjectFile As String) As List(Of ReferenceInfo)
            Dim lReferences As New List(Of ReferenceInfo)
            
            Try
                Dim lDoc As New XmlDocument()
                lDoc.Load(vProjectFile)
                
                Dim lNsMgr As New XmlNamespaceManager(lDoc.NameTable)
                lNsMgr.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003")
                
                ' Get assembly references
                Dim lAssemblyRefs As XmlNodeList = lDoc.SelectNodes("//ms:Reference[@Include]", lNsMgr)
                If lAssemblyRefs.Count = 0 Then
                    lAssemblyRefs = lDoc.SelectNodes("//Reference[@Include]")
                End If
                
                For Each lNode As XmlNode In lAssemblyRefs
                    Dim lRef As New ReferenceInfo()
                    lRef.Type = ReferenceType.eAssembly
                    lRef.Include = lNode.Attributes("Include").Value
                    
                    ' Parse assembly name and version
                    Dim lParts() As String = lRef.Include.Split(","c)
                    lRef.Name = lParts(0).Trim()
                    
                    For i As Integer = 1 To lParts.Length - 1
                        If lParts(i).Trim().StartsWith("Version=") Then
                            lRef.Version = lParts(i).Trim().Substring(8)
                        End If
                    Next
                    
                    ' Get HintPath if exists
                    Dim lHintPathNode As XmlNode = lNode.SelectSingleNode("ms:HintPath", lNsMgr)
                    If lHintPathNode Is Nothing Then
                        lHintPathNode = lNode.SelectSingleNode("HintPath")
                    End If
                    If lHintPathNode IsNot Nothing Then
                        lRef.Path = lHintPathNode.InnerText
                    End If
                    
                    lReferences.Add(lRef)
                Next
                
                ' Get package references
                Dim lPackageRefs As XmlNodeList = lDoc.SelectNodes("//PackageReference[@Include]")
                
                For Each lNode As XmlNode In lPackageRefs
                    Dim lRef As New ReferenceInfo()
                    lRef.Type = ReferenceType.ePackage
                    lRef.Name = lNode.Attributes("Include").Value
                    lRef.Include = lRef.Name
                    
                    If lNode.Attributes("Version") IsNot Nothing Then
                        lRef.Version = lNode.Attributes("Version").Value
                    End If
                    
                    lReferences.Add(lRef)
                Next
                
                ' Get project references
                Dim lProjectRefs As XmlNodeList = lDoc.SelectNodes("//ProjectReference[@Include]")
                
                For Each lNode As XmlNode In lProjectRefs
                    Dim lRef As New ReferenceInfo()
                    lRef.Type = ReferenceType.eProject
                    lRef.Path = lNode.Attributes("Include").Value
                    lRef.Include = lRef.Path
                    lRef.Name = System.IO.Path.GetFileNameWithoutExtension(lRef.Path)
                    
                    lReferences.Add(lRef)
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error getting References: {ex.Message}")
            End Try
            
            Return lReferences
        End Function
        
        ' Validate project reference (check for circular references)
        Public Function ValidateProjectReference(vProjectFile As String, vTargetProject As String) As ValidationResult
            Dim lResult As New ValidationResult()
            lResult.IsValid = True
            
            Try
                ' Check if target exists
                If Not File.Exists(vTargetProject) Then
                    lResult.IsValid = False
                    lResult.ErrorMessage = "Target project file does Not exist"
                    Return lResult
                End If
                
                ' Check if it's the same project
                If System.IO.Path.GetFullPath(vProjectFile).Equals(System.IO.Path.GetFullPath(vTargetProject), StringComparison.OrdinalIgnoreCase) Then
                    lResult.IsValid = False
                    lResult.ErrorMessage = "Cannot Reference the same project"
                    Return lResult
                End If
                
                ' Check for circular reference
                If CheckCircularReference(vProjectFile, vTargetProject) Then
                    lResult.IsValid = False
                    lResult.ErrorMessage = "Adding this Reference would create a circular dependency"
                    Return lResult
                End If
                
            Catch ex As Exception
                lResult.IsValid = False
                lResult.ErrorMessage = $"Validation error: {ex.Message}"
            End Try
            
            Return lResult
        End Function
        
        ' Private helper methods
        Private Function FindExistingReference(vDoc As XmlDocument, vNsMgr As XmlNamespaceManager, vName As String, vType As ReferenceType) As XmlNode
            Dim lNodeName As String = GetNodeNameForType(vType)
            
            ' Try with namespace
            Dim lNodes As XmlNodeList = vDoc.SelectNodes($"//ms:{lNodeName}[@Include]", vNsMgr)
            If lNodes.Count = 0 Then
                ' Try without namespace
                lNodes = vDoc.SelectNodes($"//{lNodeName}[@Include]")
            End If
            
            For Each lNode As XmlNode In lNodes
                Dim lInclude As String = lNode.Attributes("Include").Value
                
                Select Case vType
                    Case ReferenceType.eAssembly
                        ' For assemblies, compare just the name part
                        Dim lParts() As String = lInclude.Split(","c)
                        If lParts(0).Trim().Equals(vName, StringComparison.OrdinalIgnoreCase) Then
                            Return lNode
                        End If
                    Case ReferenceType.ePackage, ReferenceType.eProject
                        If lInclude.Equals(vName, StringComparison.OrdinalIgnoreCase) Then
                            Return lNode
                        End If
                End Select
            Next
            
            Return Nothing
        End Function
        
        Private Function FindOrCreateItemGroup(vDoc As XmlDocument, vNsMgr As XmlNamespaceManager, vItemType As String) As XmlNode
            ' Look for existing ItemGroup with this item type
            Dim lItemGroups As XmlNodeList = vDoc.SelectNodes($"//ms:ItemGroup[ms:{vItemType}]", vNsMgr)
            If lItemGroups.Count = 0 Then
                lItemGroups = vDoc.SelectNodes($"//ItemGroup[{vItemType}]")
            End If
            
            If lItemGroups.Count > 0 Then
                Return lItemGroups(0)
            End If
            
            ' Create new ItemGroup
            Dim lItemGroup As XmlElement = vDoc.CreateElement("ItemGroup", vDoc.DocumentElement.NamespaceURI)
            
            ' Find a good place to insert it
            Dim lProject As XmlNode = vDoc.SelectSingleNode("//ms:project", vNsMgr)
            If lProject Is Nothing Then
                lProject = vDoc.SelectSingleNode("//project")
            End If
            
            If lProject IsNot Nothing Then
                ' Insert after last ItemGroup or before first Target
                Dim lLastItemGroup As XmlNode = Nothing
                For Each lChild As XmlNode In lProject.ChildNodes
                    If lChild.Name = "ItemGroup" Then
                        lLastItemGroup = lChild
                    ElseIf lChild.Name = "Target" Then
                        lProject.InsertBefore(lItemGroup, lChild)
                        Return lItemGroup
                    End If
                Next
                
                If lLastItemGroup IsNot Nothing Then
                    lProject.InsertAfter(lItemGroup, lLastItemGroup)
                Else
                    lProject.AppendChild(lItemGroup)
                End If
            End If
            
            Return lItemGroup
        End Function
        
        Private Function GetNodeNameForType(vType As ReferenceType) As String
            Select Case vType
                Case ReferenceType.eAssembly
                    Return "Reference"
                Case ReferenceType.ePackage
                    Return "PackageReference"
                Case ReferenceType.eProject
                    Return "ProjectReference"
                Case Else
                    Return ""
            End Select
        End Function
        
        ''' <summary>
        ''' Save project file with proper XML formatting
        ''' </summary>
        ''' <param name="vDoc">XML document to save</param>
        ''' <param name="vProjectPath">Path to save the project file to</param>
        ''' <remarks>
        ''' Uses the provided project path as the destination file
        ''' </remarks>
        Private Sub SaveProjectFile(vDoc As XmlDocument, vProjectPath As String)
            Try
                If String.IsNullOrEmpty(vProjectPath) Then
                    Throw New InvalidOperationException("Project Path Is Not provided")
                End If
                
                ' Create XmlWriterSettings for proper formatting
                Dim lSettings As New XmlWriterSettings()
                lSettings.Indent = True
                lSettings.IndentChars = "  "
                lSettings.NewLineChars = Environment.NewLine
                lSettings.NewLineHandling = NewLineHandling.Replace
                lSettings.OmitXmlDeclaration = False
                lSettings.Encoding = New System.Text.UTF8Encoding(False) ' UTF-8 without BOM
                
                ' Save with XmlWriter for proper formatting
                Using lWriter As XmlWriter = XmlWriter.Create(vProjectPath, lSettings)
                    vDoc.Save(lWriter)
                End Using
                
                Console.WriteLine($"ReferenceManager saved project file: {vProjectPath}")
                
            Catch ex As Exception
                Console.WriteLine($"ReferenceManager.SaveProjectFile error: {ex.Message}")
                Throw ' Re-throw to let caller handle
            End Try
        End Sub
        
        Private Function GetRelativePath(vFromPath As String, vToPath As String) As String
            Dim lFromUri As New Uri(vFromPath & System.IO.Path.DirectorySeparatorChar)
            Dim lToUri As New Uri(vToPath)
            
            Dim lRelativeUri As Uri = lFromUri.MakeRelativeUri(lToUri)
            Dim lRelativePath As String = Uri.UnescapeDataString(lRelativeUri.ToString())
            
            ' Convert forward slashes to backslashes for Windows compatibility
            Return lRelativePath.Replace("/"c, System.IO.Path.DirectorySeparatorChar)
        End Function
        
        Private Function CheckCircularReference(vProjectFile As String, vTargetProject As String) As Boolean
            ' Simple check: see if target project references our project
            Dim lTargetRefs As List(Of ReferenceInfo) = GetAllReferences(vTargetProject)
            Dim lOurProjectName As String = System.IO.Path.GetFileNameWithoutExtension(vProjectFile)
            
            For Each lRef In lTargetRefs.Where(Function(r) r.Type = ReferenceType.eProject)
                Dim lRefProjectName As String = System.IO.Path.GetFileNameWithoutExtension(lRef.Path)
                If lRefProjectName.Equals(lOurProjectName, StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If
            Next
            
            ' TODO: Implement deeper circular reference checking
            Return False
        End Function

    End Class
End Namespace
