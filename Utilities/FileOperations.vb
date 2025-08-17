' Utilities/FileOperations.vb - File dialog creation utilities (UI only)
Imports Gtk

Namespace Utilities
    ' SIMPLIFIED: Only handles file dialog creation, no actual file I/O
    ' All file I/O is handled by FileIOManager class
    Public Module FileOperations

        ' Create dialog for opening project files
        Public Function CreateOpenProjectDialog(vParentWindow As Window) As FileChooserDialog
            Try
                Dim lDialog As New FileChooserDialog(
                    "Select VB.NET project File",
                    vParentWindow,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept
                )

                ' Add filter for project files
                Dim lProjectFilter As New FileFilter()
                lProjectFilter.Name = "VB.NET project Files (*.vbproj)"
                lProjectFilter.AddPattern("*.vbproj")
                lDialog.AddFilter(lProjectFilter)

                ' Add filter for solution files  
                Dim lSolutionFilter As New FileFilter()
                lSolutionFilter.Name = "Solution Files (*.sln)"
                lSolutionFilter.AddPattern("*.sln")
                lDialog.AddFilter(lSolutionFilter)

                ' Add filter for all files
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files (*)"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)

                Return lDialog
                
            Catch ex As Exception
                Console.WriteLine($"CreateOpenProjectDialog error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ' Create dialog for opening source files
        Public Function CreateOpenFileDialog(vParentWindow As Window) As FileChooserDialog
            Try
                Dim lDialog As New FileChooserDialog(
                    "Open File",
                    vParentWindow,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept
                )

                ' Add filter for VB.NET files
                Dim lVBFilter As New FileFilter()
                lVBFilter.Name = "VB.NET Files (*.vb)"
                lVBFilter.AddPattern("*.vb")
                lDialog.AddFilter(lVBFilter)

                ' Add filter for text files
                Dim lTextFilter As New FileFilter()
                lTextFilter.Name = "Text Files (*.txt)"
                lTextFilter.AddPattern("*.txt")
                lDialog.AddFilter(lTextFilter)

                ' Add filter for all files
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files (*)"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)

                Return lDialog
                
            Catch ex As Exception
                Console.WriteLine($"CreateOpenFileDialog error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ' Create dialog for saving files
        Public Function CreateSaveAsDialog(vParentWindow As Window) As FileChooserDialog
            Try
                Dim lDialog As New FileChooserDialog(
                    "Save File As",
                    vParentWindow,
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Save", ResponseType.Accept
                )

                ' Set overwrite confirmation
                lDialog.DoOverwriteConfirmation = True

                ' Add filter for VB.NET files
                Dim lVBFilter As New FileFilter()
                lVBFilter.Name = "VB.NET Files (*.vb)"
                lVBFilter.AddPattern("*.vb")
                lDialog.AddFilter(lVBFilter)

                ' Add filter for text files
                Dim lTextFilter As New FileFilter()
                lTextFilter.Name = "Text Files (*.txt)"
                lTextFilter.AddPattern("*.txt")
                lDialog.AddFilter(lTextFilter)

                ' Add filter for all files
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files (*)"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)

                Return lDialog
                
            Catch ex As Exception
                Console.WriteLine($"CreateSaveAsDialog error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ' Create dialog for selecting folder
        Public Function CreateSelectFolderDialog(vParentWindow As Window, vTitle As String) As FileChooserDialog
            Try
                Dim lDialog As New FileChooserDialog(
                    vTitle,
                    vParentWindow,
                    FileChooserAction.SelectFolder,
                    "Cancel", ResponseType.Cancel,
                    "Select", ResponseType.Accept
                )

                Return lDialog
                
            Catch ex As Exception
                Console.WriteLine($"CreateSelectFolderDialog error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ' Create dialog for adding existing files to project
        Public Function CreateAddFilesDialog(vParentWindow As Window) As FileChooserDialog
            Try
                Dim lDialog As New FileChooserDialog(
                    "Add Existing Files",
                    vParentWindow,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Add", ResponseType.Accept
                )

                ' Allow multiple selection
                lDialog.SelectMultiple = True

                ' Add filter for VB.NET files
                Dim lVBFilter As New FileFilter()
                lVBFilter.Name = "VB.NET Files (*.vb)"
                lVBFilter.AddPattern("*.vb")
                lDialog.AddFilter(lVBFilter)

                ' Add filter for resource files
                Dim lResourceFilter As New FileFilter()
                lResourceFilter.Name = "Resource Files (*.resx)"
                lResourceFilter.AddPattern("*.resx")
                lDialog.AddFilter(lResourceFilter)

                ' Add filter for image files
                Dim lImageFilter As New FileFilter()
                lImageFilter.Name = "Image Files"
                lImageFilter.AddPattern("*.png")
                lImageFilter.AddPattern("*.jpg")
                lImageFilter.AddPattern("*.jpeg")
                lImageFilter.AddPattern("*.gif")
                lImageFilter.AddPattern("*.bmp")
                lImageFilter.AddPattern("*.ico")
                lDialog.AddFilter(lImageFilter)

                ' Add filter for all files
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files (*)"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)

                Return lDialog
                
            Catch ex As Exception
                Console.WriteLine($"CreateAddFilesDialog error: {ex.Message}")
                Return Nothing
            End Try
        End Function

        ' Create dialog for exporting files
        Public Function CreateExportDialog(vParentWindow As Window, vDefaultName As String) As FileChooserDialog
            Try
                Dim lDialog As New FileChooserDialog(
                    "Export File",
                    vParentWindow,
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Export", ResponseType.Accept
                )

                ' Set default name
                If Not String.IsNullOrEmpty(vDefaultName) Then
                    lDialog.CurrentName = vDefaultName
                End If

                ' Set overwrite confirmation
                lDialog.DoOverwriteConfirmation = True

                ' Add filter for various export formats
                Dim lTextFilter As New FileFilter()
                lTextFilter.Name = "Text File (*.txt)"
                lTextFilter.AddPattern("*.txt")
                lDialog.AddFilter(lTextFilter)

                Dim lHtmlFilter As New FileFilter()
                lHtmlFilter.Name = "HTML File (*.html)"
                lHtmlFilter.AddPattern("*.html")
                lHtmlFilter.AddPattern("*.htm")
                lDialog.AddFilter(lHtmlFilter)

                Dim lRtfFilter As New FileFilter()
                lRtfFilter.Name = "Rich Text Format (*.rtf)"
                lRtfFilter.AddPattern("*.rtf")
                lDialog.AddFilter(lRtfFilter)

                ' Add filter for all files
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files (*)"
                lAllFilter.AddPattern("*")
                lDialog.AddFilter(lAllFilter)

                Return lDialog
                
            Catch ex As Exception
                Console.WriteLine($"CreateExportDialog error: {ex.Message}")
                Return Nothing
            End Try
        End Function

    End Module
End Namespace
