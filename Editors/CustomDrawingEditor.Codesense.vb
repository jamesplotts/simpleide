' Editors/CustomDrawingEditor.Codesense.vb - Implementation of missing IEditor interface methods
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' StartCodeSense - Start CodeSense with the given context
        Public Sub StartCodeSense(vContext As CodeSenseContext) Implements IEditor.StartCodeSense
            Try
                ' Store the context
                pCodeSenseContext = vContext
                pCodeSenseActive = True
                
                ' Create CodeSense popup if needed
                If pCodeSensePopup Is Nothing Then
                    CreateCodeSensePopup()
                End If
                
                ' Position and show the popup
                If pCodeSensePopup IsNot Nothing AndAlso vContext.SuggestedCompletions IsNot Nothing AndAlso vContext.SuggestedCompletions.Count > 0 Then
                    ' Calculate popup position based on cursor
                    Dim lX As Integer = GetCursorScreenX()
                    Dim lY As Integer = GetCursorScreenY() + pLineHeight
                    
                    ' Set popup position relative to drawing area
                    Dim lRect As New Gdk.Rectangle()
                    lRect.x = lX
                    lRect.y = lY
                    lRect.Width = 1
                    lRect.Height = 1
                    
                    pCodeSensePopup.PointingTo = lRect
                    pCodeSensePopup.ShowAll()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"StartCodeSense error: {ex.Message}")
            End Try
        End Sub
        
        ' CancelCodeSense - Cancel any active CodeSense
        Public Sub CancelCodeSense() Implements IEditor.CancelCodeSense
            Try
                pCodeSenseActive = False
                pCodeSenseContext = Nothing
                
                ' Hide the popup
                If pCodeSensePopup IsNot Nothing Then
                    pCodeSensePopup.Hide()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CancelCodeSense error: {ex.Message}")
            End Try
        End Sub
        
        ' Create the CodeSense popup
        Private Sub CreateCodeSensePopup()
            Try
                pCodeSensePopup = New Popover(pDrawingArea)
                pCodeSensePopup.Position = PositionType.Bottom
                
                ' Create a scrolled window for the list
                Dim lScrolledWindow As New ScrolledWindow()
                lScrolledWindow.SetSizeRequest(300, 200)
                lScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                
                ' Create list box for suggestions
                Dim lListBox As New ListBox()
                
                ' Add suggestions from context
                If pCodeSenseContext IsNot Nothing AndAlso pCodeSenseContext.SuggestedCompletions IsNot Nothing Then
                    For Each lSuggestion In pCodeSenseContext.SuggestedCompletions
                        Dim lRow As New ListBoxRow()
                        Dim lLabel As New Label(lSuggestion.DisplayText)
                        lLabel.Xalign = 0
                        lRow.Add(lLabel)
                        lListBox.Add(lRow)
                    Next
                End If
                
                lScrolledWindow.Add(lListBox)
                pCodeSensePopup.Add(lScrolledWindow)
                
                ' Track current selection
                Dim lCurrentIndex As Integer = 0
                
                ' Handle keyboard navigation and selection
                AddHandler pCodeSensePopup.KeyPressEvent, Sub(sender As Object, args As KeyPressEventArgs)
                    Try
                        Select Case args.Event.key
                            Case Gdk.key.Up, Gdk.key.KP_Up
                                If lCurrentIndex > 0 Then
                                    lCurrentIndex -= 1
                                    lListBox.SelectRow(lListBox.GetRowAtIndex(lCurrentIndex))
                                End If
                                args.RetVal = True
                                
                            Case Gdk.key.Down, Gdk.key.KP_Down
                                If lCurrentIndex < lListBox.Children.Length - 1 Then
                                    lCurrentIndex += 1
                                    lListBox.SelectRow(lListBox.GetRowAtIndex(lCurrentIndex))
                                End If
                                args.RetVal = True
                                
                            Case Gdk.key.Return, Gdk.key.KP_Enter
                                ' Insert selected suggestion
                                If lCurrentIndex >= 0 AndAlso lCurrentIndex < pCodeSenseContext.SuggestedCompletions.Count Then
                                    Dim lSuggestion = pCodeSenseContext.SuggestedCompletions(lCurrentIndex)
                                    InsertCodeSenseSuggestion(lSuggestion)
                                    CancelCodeSense()
                                End If
                                args.RetVal = True
                                
                            Case Gdk.key.Escape
                                CancelCodeSense()
                                args.RetVal = True
                        End Select
                    Catch ex As Exception
                        Console.WriteLine($"CodeSense key press error: {ex.Message}")
                    End Try
                End Sub
                
                ' Select first item by default
                If lListBox.Children.Length > 0 Then
                    lListBox.SelectRow(lListBox.GetRowAtIndex(0))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CreateCodeSensePopup error: {ex.Message}")
            End Try
        End Sub
        
        ' Insert the selected CodeSense suggestion
        Private Sub InsertCodeSenseSuggestion(vSuggestion As CompletionItem)
            Try
                ' This would need to be implemented based on the CodeSense context
                ' For now, just insert the text
                InsertText(vSuggestion.Text)
                
            Catch ex As Exception
                Console.WriteLine($"InsertCodeSenseSuggestion error: {ex.Message}")
            End Try
        End Sub

        ' Begin a batch update operation (suspends redrawing)
        Public Sub BeginUpdate() Implements IEditor.BeginUpdate
            Try
                pUpdateCount += 1
                
            Catch ex As Exception
                Console.WriteLine($"BeginUpdate error: {ex.Message}")
            End Try
        End Sub
        
        ' End a batch update operation (resumes redrawing)
        Public Sub EndUpdate() Implements IEditor.EndUpdate
            Try
                If pUpdateCount > 0 Then
                    pUpdateCount -= 1
                    
                    ' If updates are complete and a redraw is needed, do it now
                    If pUpdateCount = 0 AndAlso pNeedRedrawAfterUpdate Then
                        pNeedRedrawAfterUpdate = False
                        pDrawingArea?.QueueDraw()
                        pLineNumberArea?.QueueDraw()
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"EndUpdate error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Additional CodeSense Support =====
        
        ' Note: This is a modification to the existing IsModified property setter
        ' to track when updates are needed during batch operations
        Private Sub OnModifiedChanged()
            If pUpdateCount > 0 Then
                pNeedRedrawAfterUpdate = True
            End If
        End Sub

        ' CodeSense popup field
        Private pCodeSensePopup As Popover
        
        ' Show CodeSense popup
        Private Sub ShowCodeSensePopup()
            Try
                ' For now, just raise an event to let MainWindow handle it
                RaiseEvent CodeSenseRequested(Me, pCodeSenseContext)
                
            Catch ex As Exception
                Console.WriteLine($"ShowCodeSensePopup error: {ex.Message}")
            End Try
        End Sub
        
        ' Update CodeSense list
        Private Sub UpdateCodeSenseList()
            Try
                ' For now, just raise an event to let MainWindow handle it
                If pCodeSenseContext IsNot Nothing Then
                    RaiseEvent CodeSenseRequested(Me, pCodeSenseContext)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateCodeSenseList error: {ex.Message}")
            End Try
        End Sub
        
        ' Hide CodeSense popup
        Private Sub HideCodeSensePopup()
            Try
                ' Raise cancellation event
                RaiseEvent CodeSenseCancelled(Me, EventArgs.Empty)
                
                ' Clear context
                pCodeSenseContext = Nothing
                
            Catch ex As Exception
                Console.WriteLine($"HideCodeSensePopup error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
