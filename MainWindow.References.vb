' MainWindow.References.vb - Reference management integration for MainWindow
Imports Gtk
Imports System
Imports System.IO
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Managers
Imports SimpleIDE.Dialogs

Partial Public Class MainWindow
    
    ' ===== Reference Management Integration =====

    ''' <summary>
    ''' Show the Reference Manager dialog with ProjectManager integration
    ''' </summary>
    Public Sub ShowReferenceManager()
        Try
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                ShowError("No Project", "Please open a project first")
                Return
            End If
            
            ' Create dialog with ProjectManager
            Dim lDialog As New ReferenceManagerDialog(Me, pProjectManager.CurrentProjectPath, pProjectManager)
            
            ' Handle references changed event
            AddHandler lDialog.ReferencesChanged, Sub()
                OnReferencesChanged()
            End Sub
            
            ' Show the dialog
            lDialog.Run()
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"ShowReferenceManager error: {ex.Message}")
            ShowError("Reference Manager Error", ex.Message)
        End Try
    End Sub
    
    ' Show the Reference Manager dialog
    Public Sub OnManageReferences(vSender As Object, vArgs As EventArgs, Optional vInitialTab As Integer = 0)
        Try
            If String.IsNullOrEmpty(pCurrentProject) Then
                ShowError("No project", "Please open a project before managing References.")
                Return
            End If
            
            ' Create and show the reference manager dialog
            Dim lDialog As New ReferenceManagerDialog(Me, pCurrentProject, pProjectManager)
            
            ' Select initial tab if specified
            If vInitialTab >= 0 AndAlso vInitialTab < lDialog.Notebook.NPages Then
                lDialog.Notebook.Page = vInitialTab
            End If
            
            ' Handle references changed event
            AddHandler lDialog.ReferencesChanged, AddressOf OnReferencesChanged
            
            ' Show dialog
            If lDialog.Run() = CInt(ResponseType.Ok) Then
                ' References were modified
                RefreshProjectExplorer()
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"OnManageReferences error: {ex.Message}")
            ShowError("Reference Manager error", ex.Message)
        End Try
    End Sub
    
    ' Handle references changed event from dialog or project explorer
    Private Sub OnReferencesChanged()
        Try
            ' Refresh project explorer to show updated references
            If pProjectExplorer IsNot Nothing Then
                pProjectExplorer.RefreshReferences()
            End If
            
            ' Mark project as modified
            MarkProjectModified()
            
            ' Update status bar
            UpdateStatusBar("References updated")
            
            ' Trigger a re-parse of the project for CodeSense
            UpdateCodeSenseReferences()
            
        Catch ex As Exception
            Console.WriteLine($"OnReferencesChanged error: {ex.Message}")
        End Try
    End Sub
    
    
    ' Handle add reference requests from various sources
    Public Sub OnAddReference(vReferenceType As ReferenceManager.ReferenceType)
        Try
            ' Map reference type to tab index
            Dim lTabIndex As Integer = 0
            Select Case vReferenceType
                Case ReferenceManager.ReferenceType.eAssembly
                    lTabIndex = 0
                Case ReferenceManager.ReferenceType.ePackage
                    lTabIndex = 1
                Case ReferenceManager.ReferenceType.eProject
                    lTabIndex = 2
            End Select
            
            ' Show reference manager with appropriate tab
            OnManageReferences(Nothing, Nothing, lTabIndex)
            
        Catch ex As Exception
            Console.WriteLine($"OnAddReference error: {ex.Message}")
        End Try
    End Sub
    
    ' Quick add assembly reference
    Public Sub OnQuickAddAssembly()
        Try
            Dim lDialog As FileChooserDialog = New FileChooserDialog(
                "Select Assembly",
                Me,
                FileChooserAction.Open,
                "Cancel", ResponseType.Cancel,
                "Add", ResponseType.Accept
            )
            
            ' Add filters
            Dim lFilter As New FileFilter()
            lFilter.Name = "Assemblies (*.dll)"
            lFilter.AddPattern("*.dll")
            lDialog.AddFilter(lFilter)
            
            Dim lAllFilter As New FileFilter()
            lAllFilter.Name = "All Files"
            lAllFilter.AddPattern("*")
            lDialog.AddFilter(lAllFilter)
            
            ' Set initial directory
            If Not String.IsNullOrEmpty(pCurrentProject) Then
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pCurrentProject)
                lDialog.SetCurrentFolder(lProjectDir)
            End If
            
            If lDialog.Run() = CInt(ResponseType.Accept) Then
                Dim lAssemblyPath As String = lDialog.FileName
                AddAssemblyReference(lAssemblyPath)
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"OnQuickAddAssembly error: {ex.Message}")
            ShowError("Add Assembly error", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Add assembly reference through ProjectManager
    ''' </summary>
    ''' <param name="vAssemblyPath">Path to the assembly</param>
    Private Sub AddAssemblyReference(vAssemblyPath As String)
        Try
            ' Validate we have a project manager
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                ShowError("No Project", "Please open a project first")
                Return
            End If
            
            ' Validate the reference
            Dim lValidation As ReferenceManager.ValidationResult = ValidateAssemblyPath(vAssemblyPath)
            
            If Not lValidation.IsValid Then
                ShowError("Invalid Reference", lValidation.ErrorMessage)
                Return
            End If
            
            ' Add the reference through ProjectManager
            Dim lAssemblyName As String = System.IO.Path.GetFileNameWithoutExtension(vAssemblyPath)
            If pProjectManager.AddAssemblyReference(lAssemblyName, vAssemblyPath) Then
                OnReferencesChanged()
                UpdateStatusBar($"Added Reference to {System.IO.Path.GetFileName(vAssemblyPath)}")
            Else
                ShowError("Add Reference Failed", "Failed to add assembly Reference")
            End If
            
        Catch ex As Exception
            Console.WriteLine($"AddAssemblyReference error: {ex.Message}")
            ShowError("Add Reference error", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Remove reference through ProjectManager
    ''' </summary>
    ''' <param name="vReferenceInfo">Reference to remove</param>
    Public Sub RemoveReference(vReferenceInfo As ReferenceManager.ReferenceInfo)
        Try
            ' Validate we have a project manager
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                ShowError("No Project", "Please open a project first")
                Return
            End If
            
            Dim lDialog As New MessageDialog(
                Me,
                DialogFlags.Modal,
                MessageType.Question,
                ButtonsType.YesNo,
                $"Are you sure you want to remove the Reference to '{vReferenceInfo.Name}'?"
            )
            
            If lDialog.Run() = CInt(ResponseType.Yes) Then
                ' Remove through ProjectManager
                If pProjectManager.RemoveReference(vReferenceInfo.Name, vReferenceInfo.Type) Then
                    OnReferencesChanged()
                    UpdateStatusBar($"Removed Reference To {vReferenceInfo.Name}")
                Else
                    ShowError("Remove Reference Failed", $"Failed To remove Reference To {vReferenceInfo.Name}")
                End If
            End If
            
            lDialog.Destroy()
            
        Catch ex As Exception
            Console.WriteLine($"RemoveReference error: {ex.Message}")
            ShowError("Remove Reference error", ex.Message)
        End Try
    End Sub
    
    ''' <summary>
    ''' Check for missing references through ProjectManager
    ''' </summary>
    ''' <returns>True if all references are valid</returns>
    Public Function CheckMissingReferences() As Boolean
        Try
            ' Validate we have a project manager
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then
                Return True
            End If
            
            ' Get references through ProjectManager
            Dim lReferences As List(Of ReferenceManager.ReferenceInfo) = pProjectManager.ProjectReferences
            Dim lMissingRefs As New List(Of String)
            
            For Each lRef In lReferences
                Dim lValidation As ReferenceManager.ValidationResult = ValidateAssemblyPath(lRef.Path)
                If Not lValidation.IsValid Then
                    lMissingRefs.Add($"{lRef.Name} ({lRef.Type})")
                End If
            Next
            
            If lMissingRefs.Count > 0 Then
                Dim lMessage As String = "The following References are missing:" & Environment.NewLine & Environment.NewLine
                lMessage &= String.Join(Environment.NewLine, lMissingRefs)
                lMessage &= Environment.NewLine & Environment.NewLine & "Would you Like To manage References now?"
                
                Dim lDialog As New MessageDialog(
                    Me,
                    DialogFlags.Modal,
                    MessageType.Question,
                    ButtonsType.YesNo,
                    lMessage
                )
                
                Dim lResult As Boolean = (lDialog.Run() = CInt(ResponseType.Yes))
                lDialog.Destroy()
                
                If lResult Then
                    ShowReferenceManager()
                End If
                
                Return lMissingRefs.Count = 0
            End If
            
            Return True
            
        Catch ex As Exception
            Console.WriteLine($"CheckMissingReferences error: {ex.Message}")
            Return False
        End Try
    End Function
        
    ' Initialize reference management for project explorer
    Private Sub InitializeReferenceManagement()
        Try
            If pProjectExplorer IsNot Nothing Then
                ' Handle reference-related events from project explorer
                AddHandler pProjectExplorer.ReferencesChanged, AddressOf OnReferencesChanged
            End If
            
        Catch ex As Exception
            Console.WriteLine($"InitializeReferenceManagement error: {ex.Message}")
        End Try
    End Sub

    ' ===== Reference Validation =====
    
    ''' <summary>
    ''' Validates an assembly path for adding as a reference
    ''' </summary>
    Private Function ValidateAssemblyPath(vAssemblyPath As String) As ReferenceManager.ValidationResult
        Dim lResult As New ReferenceManager.ValidationResult()
        lResult.IsValid = True
        
        Try
            ' Check if file exists
            If String.IsNullOrEmpty(vAssemblyPath) Then
                lResult.IsValid = False
                lResult.ErrorMessage = "Assembly Path cannot be empty"
                Return lResult
            End If
            
            If Not File.Exists(vAssemblyPath) Then
                lResult.IsValid = False
                lResult.ErrorMessage = "Assembly file does Not exist"
                Return lResult
            End If
            
            ' Check if it's a valid assembly file
            Dim lExtension As String = System.IO.Path.GetExtension(vAssemblyPath).ToLower()
            If lExtension <> ".dll" AndAlso lExtension <> ".exe" Then
                lResult.IsValid = False
                lResult.ErrorMessage = "File must be a .dll Or .exe assembly"
                Return lResult
            End If
            
            ' Could add additional validation here such as:
            ' - Loading the assembly to verify it's valid
            ' - Checking assembly compatibility
            ' - Verifying it's not already referenced
            
        Catch ex As Exception
            lResult.IsValid = False
            lResult.ErrorMessage = $"Validation error: {ex.Message}"
        End Try
        
        Return lResult
    End Function

    
End Class
