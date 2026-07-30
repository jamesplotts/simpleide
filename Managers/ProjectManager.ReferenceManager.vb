' ProjectManager.ReferenceManager.vb - Integration of ReferenceManager with ProjectManager
' This should be a new partial class file for ProjectManager

Imports System
Imports System.IO
Imports System.Collections.Generic
Imports System.Xml
Imports SimpleIDE.Models

Namespace Managers
    
    ''' <summary>
    ''' ProjectManager extension for reference management functionality
    ''' </summary>
    Partial Public Class ProjectManager
        
        ' ===== Private Fields =====
        Private pReferenceManager As ReferenceManager
        Private pProjectReferences As List(Of ReferenceManager.ReferenceInfo)
        
        ' ===== Events =====
        
        ''' <summary>
        ''' Raised when project references change
        ''' </summary>
        Public Event ReferencesChanged(vReferences As List(Of ReferenceManager.ReferenceInfo))
        
        ''' <summary>
        ''' Raised when a reference is added
        ''' </summary>
        Public Event ReferenceAdded(vReference As ReferenceManager.ReferenceInfo)
        
        ''' <summary>
        ''' Raised when a reference is removed
        ''' </summary>
        Public Event ReferenceRemoved(vReferenceName As String, vReferenceType As ReferenceManager.ReferenceType)
        
        ' ===== Properties =====
        
        ''' <summary>
        ''' Gets the ReferenceManager instance for this project
        ''' </summary>
        ''' <value>The ReferenceManager used for all reference operations</value>
        Public ReadOnly Property ReferenceManager As ReferenceManager
            Get
                ' Lazy initialization
                If pReferenceManager Is Nothing Then
                    pReferenceManager = New ReferenceManager()
                    Console.WriteLine("ProjectManager: Initialized ReferenceManager")
                End If
                Return pReferenceManager
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the current project references
        ''' </summary>
        Public ReadOnly Property ProjectReferences As List(Of ReferenceManager.ReferenceInfo)
            Get
                Return If(pProjectReferences, New List(Of ReferenceManager.ReferenceInfo)())
            End Get
        End Property
        
        ' ===== Public Methods - Reference Management =====
        
        ''' <summary>
        ''' Loads all references for the current project
        ''' </summary>
        Public Function LoadProjectReferences() As Boolean
            Try
                If Not pIsProjectOpen OrElse String.IsNullOrEmpty(CurrentProjectPath) Then
                    Console.WriteLine("ProjectManager: No project open to load references")
                    Return False
                End If
                
                ' Use the ReferenceManager to get all references
                pProjectReferences = ReferenceManager.GetAllReferences(CurrentProjectPath)
                
                Console.WriteLine($"ProjectManager: Loaded {pProjectReferences.Count} references")
                
                ' Raise event
                RaiseEvent ReferencesChanged(pProjectReferences)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.LoadProjectReferences error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Adds an assembly reference to the project
        ''' </summary>
        ''' <param name="vAssemblyName">Name of the assembly</param>
        ''' <param name="vHintPath">Optional hint path to the assembly</param>
        ''' <returns>True if successfully added</returns>
        Public Function AddAssemblyReference(vAssemblyName As String, vHintPath As String) As Boolean
            Try
                If Not pIsProjectOpen OrElse String.IsNullOrEmpty(CurrentProjectPath) Then
                    Console.WriteLine("ProjectManager: No project open")
                    Return False
                End If
                
                ' Add through ReferenceManager
                Dim lSuccess As Boolean = ReferenceManager.AddAssemblyReference(CurrentProjectPath, vAssemblyName, vHintPath)
                
                If lSuccess Then
                    ' Reload references
                    LoadProjectReferences()
                    
                    ' Create reference info for event
                    Dim lRef As New ReferenceManager.ReferenceInfo with {
                        .Name = vAssemblyName,
                        .Type = ReferenceManager.ReferenceType.eAssembly,
                        .Path = vHintPath
                    }
                    
                    ' Raise events
                    RaiseEvent ReferenceAdded(lRef)
                    RaiseEvent ProjectModified()
                    
                    Console.WriteLine($"ProjectManager: Added assembly reference {vAssemblyName}")
                End If
                
                Return lSuccess
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.AddAssemblyReference error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Adds a package reference to the project
        ''' </summary>
        ''' <param name="vPackageName">Name of the NuGet package</param>
        ''' <param name="vVersion">Version of the package</param>
        ''' <returns>True if successfully added</returns>
        Public Function AddPackageReference(vPackageName As String, vVersion As String) As Boolean
            Try
                If Not pIsProjectOpen OrElse String.IsNullOrEmpty(CurrentProjectPath) Then
                    Console.WriteLine("ProjectManager: No project open")
                    Return False
                End If
                
                ' Add through ReferenceManager
                Dim lSuccess As Boolean = ReferenceManager.AddPackageReference(CurrentProjectPath, vPackageName, vVersion)
                
                If lSuccess Then
                    ' Reload references
                    LoadProjectReferences()
                    
                    ' Create reference info for event
                    Dim lRef As New ReferenceManager.ReferenceInfo with {
                        .Name = vPackageName,
                        .Type = ReferenceManager.ReferenceType.ePackage,
                        .Version = vVersion
                    }
                    
                    ' Raise events
                    RaiseEvent ReferenceAdded(lRef)
                    RaiseEvent ProjectModified()
                    
                    Console.WriteLine($"ProjectManager: Added package reference {vPackageName} v{vVersion}")
                End If
                
                Return lSuccess
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.AddPackageReference error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Adds a project reference to the project
        ''' </summary>
        ''' <param name="vReferencePath">Path to the project to reference</param>
        ''' <returns>True if successfully added</returns>
        Public Function AddProjectReference(vReferencePath As String) As Boolean
            Try
                If Not pIsProjectOpen OrElse String.IsNullOrEmpty(CurrentProjectPath) Then
                    Console.WriteLine("ProjectManager: No project open")
                    Return False
                End If
                
                ' Add through ReferenceManager
                Dim lSuccess As Boolean = ReferenceManager.AddProjectReference(CurrentProjectPath, vReferencePath)
                
                If lSuccess Then
                    ' Reload references
                    LoadProjectReferences()
                    
                    ' Create reference info for event
                    Dim lRef As New ReferenceManager.ReferenceInfo with {
                        .Name = System.IO.Path.GetFileNameWithoutExtension(vReferencePath),
                        .Type = ReferenceManager.ReferenceType.eProject,
                        .Path = vReferencePath
                    }
                    
                    ' Raise events
                    RaiseEvent ReferenceAdded(lRef)
                    RaiseEvent ProjectModified()
                    
                    Console.WriteLine($"ProjectManager: Added project reference {lRef.Name}")
                End If
                
                Return lSuccess
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.AddProjectReference error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Removes a reference from the project
        ''' </summary>
        ''' <param name="vReferenceName">Name of the reference to remove</param>
        ''' <param name="vReferenceType">Type of the reference</param>
        ''' <returns>True if successfully removed</returns>
        Public Function RemoveReference(vReferenceName As String, vReferenceType As ReferenceManager.ReferenceType) As Boolean
            Try
                If Not pIsProjectOpen OrElse String.IsNullOrEmpty(CurrentProjectPath) Then
                    Console.WriteLine("ProjectManager: No project open")
                    Return False
                End If
                
                ' Remove through ReferenceManager
                Dim lSuccess As Boolean = ReferenceManager.RemoveReference(CurrentProjectPath, vReferenceName, vReferenceType)
                
                If lSuccess Then
                    ' Reload references
                    LoadProjectReferences()
                    
                    ' Raise events
                    RaiseEvent ReferenceRemoved(vReferenceName, vReferenceType)
                    RaiseEvent ProjectModified()
                    
                    Console.WriteLine($"ProjectManager: Removed reference {vReferenceName}")
                End If
                
                Return lSuccess
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.RemoveReference error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Validates a project reference for circular dependencies
        ''' </summary>
        ''' <param name="vTargetProject">Path to the project to reference</param>
        ''' <returns>ValidationResult with details</returns>
        Public Function ValidateProjectReference(vTargetProject As String) As ReferenceManager.ValidationResult
            Try
                If Not pIsProjectOpen OrElse String.IsNullOrEmpty(CurrentProjectPath) Then
                    Return New ReferenceManager.ValidationResult with {
                        .IsValid = False,
                        .ErrorMessage = "No project is currently open"
                    }
                End If
                
                ' Validate through ReferenceManager
                Return ReferenceManager.ValidateProjectReference(CurrentProjectPath, vTargetProject)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.ValidateProjectReference error: {ex.Message}")
                Return New ReferenceManager.ValidationResult with {
                    .IsValid = False,
                    .ErrorMessage = $"Validation error: {ex.Message}"
                }
            End Try
        End Function
        
        ''' <summary>
        ''' Gets references of a specific type
        ''' </summary>
        ''' <param name="vType">Type of references to get</param>
        ''' <returns>List of references of the specified type</returns>
        Public Function GetReferencesByType(vType As ReferenceManager.ReferenceType) As List(Of ReferenceManager.ReferenceInfo)
            Try
                If pProjectReferences Is Nothing Then
                    LoadProjectReferences()
                End If
                
                Return pProjectReferences.Where(Function(r) r.Type = vType).ToList()
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.GetReferencesByType error: {ex.Message}")
                Return New List(Of ReferenceManager.ReferenceInfo)()
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if a reference exists
        ''' </summary>
        ''' <param name="vReferenceName">Name of the reference</param>
        ''' <param name="vType">Type of the reference</param>
        ''' <returns>True if the reference exists</returns>
        Public Function HasReference(vReferenceName As String, vType As ReferenceManager.ReferenceType) As Boolean
            Try
                If pProjectReferences Is Nothing Then
                    LoadProjectReferences()
                End If
                
                Return pProjectReferences.Any(Function(r) r.Name = vReferenceName AndAlso r.Type = vType)
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.HasReference error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Private Helper Methods =====
        
        ''' <summary>
        ''' Called when project is loaded to initialize references
        ''' </summary>
        Private Sub InitializeProjectReferences()
            Try
                ' Load all references for the project
                LoadProjectReferences()
                
                Console.WriteLine($"ProjectManager: Initialized {pProjectReferences.Count} references for project")
                
            Catch ex As Exception
                Console.WriteLine($"ProjectManager.InitializeProjectReferences error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace 
