' Editors/CustomDrawingEditor.Intellisense.vb - Implementation of missing IEditor interface methods
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

Namespace Editors

    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor

        ' StartIntelliSense - Start IntelliSense with the given context
        Public Sub StartIntelliSense(vContext As IntelliSenseContext) Implements IEditor.StartIntelliSense
            Try
                ' Store the context
                pIntelliSenseContext = vContext
                pIntelliSenseActive = True
                
                ' Create IntelliSense popup if needed
                If pIntelliSensePopup Is Nothing Then
                    CreateIntelliSensePopup()
                End If
                
                ' Position and show the popup
                If pIntelliSensePopup IsNot Nothing AndAlso vContext.SuggestedCompletions IsNot Nothing AndAlso vContext.SuggestedCompletions.Count > 0 Then
                    ' Calculate popup position based on cursor
                    Dim lX As Integer = GetCursorScreenX()
                    Dim lY As Integer = GetCursorScreenY() + pLineHeight
                    
                    ' Set popup position relative to drawing area
                    Dim lRect As New Gdk.Rectangle()
                    lRect.x = lX
                    lRect.y = lY
                    lRect.Width = 1
                    lRect.Height = 1
                    
                    pIntelliSensePopup.PointingTo = lRect
                    pIntelliSensePopup.ShowAll()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"StartIntelliSense error: {ex.Message}")
            End Try
        End Sub
        
        ' CancelIntelliSense - Cancel any active IntelliSense
        Public Sub CancelIntelliSense() Implements IEditor.CancelIntelliSense
            Try
                pIntelliSenseActive = False
                pIntelliSenseContext = Nothing
                
                ' Hide the popup
                If pIntelliSensePopup IsNot Nothing Then
                    pIntelliSensePopup.Hide()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CancelIntelliSense error: {ex.Message}")
            End Try
        End Sub
        
        ' Create the IntelliSense popup
        Private Sub CreateIntelliSensePopup()
            Try
                pIntelliSensePopup = New Popover(pDrawingArea)
                pIntelliSensePopup.Position = PositionType.Bottom
                
                ' Create a scrolled window for the list
                Dim lScrolledWindow As New ScrolledWindow()
                lScrolledWindow.SetSizeRequest(300, 200)
                lScrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                
                ' Create list box for suggestions
                Dim lListBox As New ListBox()
                
                ' Add suggestions from context
                If pIntelliSenseContext IsNot Nothing AndAlso pIntelliSenseContext.SuggestedCompletions IsNot Nothing Then
                    For Each lSuggestion In pIntelliSenseContext.SuggestedCompletions
                        Dim lRow As New ListBoxRow()
                        Dim lLabel As New Label(lSuggestion.DisplayText)
                        lLabel.Xalign = 0
                        lRow.Add(lLabel)
                        lListBox.Add(lRow)
                    Next
                End If
                
                lScrolledWindow.Add(lListBox)
                pIntelliSensePopup.Add(lScrolledWindow)
                
                ' Track current selection
                Dim lCurrentIndex As Integer = 0
                
                ' Handle keyboard navigation and selection
                AddHandler pIntelliSensePopup.KeyPressEvent, Sub(sender As Object, args As KeyPressEventArgs)
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
                                If lCurrentIndex >= 0 AndAlso lCurrentIndex < pIntelliSenseContext.SuggestedCompletions.Count Then
                                    Dim lSuggestion = pIntelliSenseContext.SuggestedCompletions(lCurrentIndex)
                                    InsertIntelliSenseSuggestion(lSuggestion)
                                    CancelIntelliSense()
                                End If
                                args.RetVal = True
                                
                            Case Gdk.key.Escape
                                CancelIntelliSense()
                                args.RetVal = True
                        End Select
                    Catch ex As Exception
                        Console.WriteLine($"IntelliSense key press error: {ex.Message}")
                    End Try
                End Sub
                
                ' Select first item by default
                If lListBox.Children.Length > 0 Then
                    lListBox.SelectRow(lListBox.GetRowAtIndex(0))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CreateIntelliSensePopup error: {ex.Message}")
            End Try
        End Sub
        
        ' Insert the selected IntelliSense suggestion
        Private Sub InsertIntelliSenseSuggestion(vSuggestion As CompletionItem)
            Try
                ' This would need to be implemented based on the IntelliSense context
                ' For now, just insert the text
                InsertText(vSuggestion.Text)
                
            Catch ex As Exception
                Console.WriteLine($"InsertIntelliSenseSuggestion error: {ex.Message}")
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
        
        ' ===== Additional IntelliSense Support =====
        
        ' Note: This is a modification to the existing IsModified property setter
        ' to track when updates are needed during batch operations
        Private Sub OnModifiedChanged()
            If pUpdateCount > 0 Then
                pNeedRedrawAfterUpdate = True
            End If
        End Sub

        ' IntelliSense popup field
        Private pIntelliSensePopup As Popover
        
        ' Show IntelliSense popup
        Private Sub ShowIntelliSensePopup()
            Try
                ' For now, just raise an event to let MainWindow handle it
                RaiseEvent IntelliSenseRequested(Me, pIntelliSenseContext)
                
            Catch ex As Exception
                Console.WriteLine($"ShowIntelliSensePopup error: {ex.Message}")
            End Try
        End Sub
        
        ' Update IntelliSense list
        Private Sub UpdateIntelliSenseList()
            Try
                ' For now, just raise an event to let MainWindow handle it
                If pIntelliSenseContext IsNot Nothing Then
                    RaiseEvent IntelliSenseRequested(Me, pIntelliSenseContext)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIntelliSenseList error: {ex.Message}")
            End Try
        End Sub
        
        ' Hide IntelliSense popup
        Private Sub HideIntelliSensePopup()
            Try
                ' Raise cancellation event
                RaiseEvent IntelliSenseCancelled(Me, EventArgs.Empty)
                
                ' Clear context
                pIntelliSenseContext = Nothing
                
            Catch ex As Exception
                Console.WriteLine($"HideIntelliSensePopup error: {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
