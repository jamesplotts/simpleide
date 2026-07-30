' Models/CharacterToken.vb - Byte-encoded character token information
Imports System

Namespace Models
    
    ''' <summary>
    ''' Provides byte encoding and decoding for character token information
    ''' </summary>
    ''' <remarks>
    ''' This module encodes syntax token type and style information into a single byte:
    ''' Bit 7: Reserved/Extension flag
    ''' Bit 6: Italic flag
    ''' Bit 5: Bold flag
    ''' Bits 0-4: Token Type (supports up to 32 types)
    ''' </remarks>
    Public Module CharacterToken
        
        ' ===== Bit Masks =====
        
        ''' <summary>
        ''' Mask for token type bits (bits 0-4)
        ''' </summary>
        Public Const TOKEN_TYPE_MASK As Byte = &H1F      ' 00011111
        
        ''' <summary>
        ''' Bit flag for bold text (bit 5)
        ''' </summary>
        Public Const TOKEN_BOLD_BIT As Byte = &H20       ' 00100000
        
        ''' <summary>
        ''' Bit flag for italic text (bit 6)
        ''' </summary>
        Public Const TOKEN_ITALIC_BIT As Byte = &H40     ' 01000000
        
        ''' <summary>
        ''' Reserved bit for future extensions (bit 7)
        ''' </summary>
        Public Const TOKEN_RESERVED_BIT As Byte = &H80   ' 10000000
        
        ' ===== Encoding Functions =====
        
        ''' <summary>
        ''' Encodes token type and style information into a single byte
        ''' </summary>
        ''' <param name="vTokenType">The syntax token type</param>
        ''' <param name="vBold">Whether the text should be bold</param>
        ''' <param name="vItalic">Whether the text should be italic</param>
        ''' <returns>Encoded byte containing all information</returns>
        Public Function Encode(vTokenType As SyntaxTokenType, 
                              Optional vBold As Boolean = False,
                              Optional vItalic As Boolean = False) As Byte
            Try
                ' Start with token type (masked to 5 bits)
                Dim lResult As Byte = CByte(vTokenType) And TOKEN_TYPE_MASK
                
                ' Add style flags
                If vBold Then lResult = lResult Or TOKEN_BOLD_BIT
                If vItalic Then lResult = lResult Or TOKEN_ITALIC_BIT
                
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"CharacterToken.Encode error: {ex.Message}")
                Return CByte(SyntaxTokenType.eNormal)
            End Try
        End Function
        
        ''' <summary>
        ''' Encodes just a token type with no style information
        ''' </summary>
        ''' <param name="vTokenType">The syntax token type</param>
        ''' <returns>Encoded byte with token type only</returns>
        Public Function EncodeType(vTokenType As SyntaxTokenType) As Byte
            Return CByte(vTokenType) And TOKEN_TYPE_MASK
        End Function
        
        ' ===== Decoding Functions =====
        
        ''' <summary>
        ''' Extracts the token type from an encoded byte
        ''' </summary>
        ''' <param name="vEncodedByte">The encoded byte</param>
        ''' <returns>The SyntaxTokenType</returns>
        Public Function GetTokenType(vEncodedByte As Byte) As SyntaxTokenType
            Try
                Dim lTypeValue As Integer = vEncodedByte And TOKEN_TYPE_MASK
                
                ' Validate the value is within enum range
                If lTypeValue >= 0 AndAlso lTypeValue < SyntaxTokenType.eLastValue Then
                    Return CType(lTypeValue, SyntaxTokenType)
                Else
                    Return SyntaxTokenType.eNormal
                End If
                
            Catch ex As Exception
                Console.WriteLine($"CharacterToken.GetTokenType error: {ex.Message}")
                Return SyntaxTokenType.eNormal
            End Try
        End Function
        
        ''' <summary>
        ''' Checks if the encoded byte indicates bold text
        ''' </summary>
        ''' <param name="vEncodedByte">The encoded byte</param>
        ''' <returns>True if bold flag is set, False otherwise</returns>
        Public Function IsBold(vEncodedByte As Byte) As Boolean
            Return (vEncodedByte And TOKEN_BOLD_BIT) <> 0
        End Function
        
        ''' <summary>
        ''' Checks if the encoded byte indicates italic text
        ''' </summary>
        ''' <param name="vEncodedByte">The encoded byte</param>
        ''' <returns>True if italic flag is set, False otherwise</returns>
        Public Function IsItalic(vEncodedByte As Byte) As Boolean
            Return (vEncodedByte And TOKEN_ITALIC_BIT) <> 0
        End Function
        
        ''' <summary>
        ''' Checks if the reserved bit is set (for future extensions)
        ''' </summary>
        ''' <param name="vEncodedByte">The encoded byte</param>
        ''' <returns>True if reserved bit is set, False otherwise</returns>
        Public Function IsReserved(vEncodedByte As Byte) As Boolean
            Return (vEncodedByte And TOKEN_RESERVED_BIT) <> 0
        End Function
        
        ' ===== Style Modification Functions =====
        
        ''' <summary>
        ''' Sets the bold flag on an encoded byte
        ''' </summary>
        ''' <param name="vEncodedByte">The encoded byte to modify</param>
        ''' <returns>Modified byte with bold flag set</returns>
        Public Function SetBold(vEncodedByte As Byte) As Byte
            Return vEncodedByte Or TOKEN_BOLD_BIT
        End Function
        
        ''' <summary>
        ''' Clears the bold flag on an encoded byte
        ''' </summary>
        ''' <param name="vEncodedByte">The encoded byte to modify</param>
        ''' <returns>Modified byte with bold flag cleared</returns>
        Public Function ClearBold(vEncodedByte As Byte) As Byte
            Return vEncodedByte And (Not TOKEN_BOLD_BIT)
        End Function
        
        ''' <summary>
        ''' Sets the italic flag on an encoded byte
        ''' </summary>
        ''' <param name="vEncodedByte">The encoded byte to modify</param>
        ''' <returns>Modified byte with italic flag set</returns>
        Public Function SetItalic(vEncodedByte As Byte) As Byte
            Return vEncodedByte Or TOKEN_ITALIC_BIT
        End Function
        
        ''' <summary>
        ''' Clears the italic flag on an encoded byte
        ''' </summary>
        ''' <param name="vEncodedByte">The encoded byte to modify</param>
        ''' <returns>Modified byte with italic flag cleared</returns>
        Public Function ClearItalic(vEncodedByte As Byte) As Byte
            Return vEncodedByte And (Not TOKEN_ITALIC_BIT)
        End Function
        
        ' ===== Utility Functions =====
        
        ''' <summary>
        ''' Creates a default token byte for normal text
        ''' </summary>
        ''' <returns>Encoded byte representing normal text with no styling</returns>
        Public Function CreateDefault() As Byte
            Return CByte(SyntaxTokenType.eNormal)
        End Function
        
        ''' <summary>
        ''' Decodes a byte into a human-readable string for debugging
        ''' </summary>
        ''' <param name="vEncodedByte">The encoded byte</param>
        ''' <returns>String representation of the encoded information</returns>
        Public Function ToString(vEncodedByte As Byte) As String
            Try
                Dim lType As SyntaxTokenType = GetTokenType(vEncodedByte)
                Dim lBold As Boolean = IsBold(vEncodedByte)
                Dim lItalic As Boolean = IsItalic(vEncodedByte)
                
                Dim lResult As String = lType.ToString()
                If lBold Then lResult &= " [Bold]"
                If lItalic Then lResult &= " [Italic]"
                
                Return lResult
                
            Catch ex As Exception
                Return $"Invalid({vEncodedByte:X2})"
            End Try
        End Function
        
    End Module
    
End Namespace