' Editors/CustomDrawingEditor.Codesense.vb - IEditor CodeSense interface methods and batch-update tracking
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ''' <summary>
        ''' Starts CodeSense with the given context, showing the popup drawn on the editor surface
        ''' </summary>
        ''' <param name="vContext">Context carrying the completion items to display</param>
        Public Sub StartCodeSense(vContext As CodeSenseContext) Implements IEditor.StartCodeSense
            Try
                If vContext Is Nothing OrElse vContext.SuggestedCompletions Is Nothing OrElse vContext.SuggestedCompletions.Count = 0 Then
                    HideCodeSensePopup()
                    Return
                End If

                Dim lSuggestions As New List(Of CodeSenseSuggestion)()
                for each lItem in vContext.SuggestedCompletions
                    Dim lSuggestion As New CodeSenseSuggestion()
                    lSuggestion.Text = lItem.Text
                    lSuggestion.Description = lItem.Description
                    lSuggestion.Icon = lItem.Icon
                    lSuggestions.Add(lSuggestion)
                Next

                ShowCodeSenseSuggestions(lSuggestions, vContext)

            Catch ex As Exception
                Console.WriteLine($"StartCodeSense error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Cancels any active CodeSense popup
        ''' </summary>
        Public Sub CancelCodeSense() Implements IEditor.CancelCodeSense
            Try
                HideCodeSensePopup()
                RaiseEvent CodeSenseCancelled(Me, EventArgs.Empty)

            Catch ex As Exception
                Console.WriteLine($"CancelCodeSense error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Begins a batch update operation (suspends redrawing)
        ''' </summary>
        Public Sub BeginUpdate() Implements IEditor.BeginUpdate
            Try
                pUpdateCount += 1

            Catch ex As Exception
                Console.WriteLine($"BeginUpdate error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Ends a batch update operation (resumes redrawing)
        ''' </summary>
        Public Sub EndUpdate() Implements IEditor.EndUpdate
            Try
                If pUpdateCount > 0 Then
                    pUpdateCount -= 1

                    If pUpdateCount = 0 AndAlso pNeedRedrawAfterUpdate Then
                        pNeedRedrawAfterUpdate = False
                        pDrawingArea?.QueueDraw()
                    End If
                End If

            Catch ex As Exception
                Console.WriteLine($"EndUpdate error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Tracks that a redraw is needed once the current batch update completes
        ''' </summary>
        Private Sub OnModifiedChanged()
            If pUpdateCount > 0 Then
                pNeedRedrawAfterUpdate = True
            End If
        End Sub

    End Class

End Namespace
