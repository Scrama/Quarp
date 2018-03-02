using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Quarp.Image
{
    /// <summary>
    /// Reads and loads a Truevision TGA Format image file.
    /// </summary>
    internal class TargaImage : IDisposable
    {
        internal static class TargaConstants
        {
            // constant byte lengths for various fields in the Targa format
            internal const int HeaderByteLength = 18;
            internal const int FooterByteLength = 26;
            internal const int FooterSignatureOffsetFromEnd = 18;
            internal const int FooterSignatureByteLength = 16;
            internal const int FooterReservedCharByteLength = 1;
            internal const int ExtensionAreaAuthorNameByteLength = 41;
            internal const int ExtensionAreaAuthorCommentsByteLength = 324;
            internal const int ExtensionAreaJobNameByteLength = 41;
            internal const int ExtensionAreaSoftwareIdByteLength = 41;
            internal const int ExtensionAreaSoftwareVersionLetterByteLength = 1;
            internal const int ExtensionAreaColorCorrectionTableValueLength = 256;
            internal const string TargaFooterAsciiSignature = "TRUEVISION-XFILE";
        }


        /// <summary>
        /// The Targa format of the file.
        /// </summary>
        public enum TgaFormat
        {
            /// <summary>
            /// Unknown Targa Image format.
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// Original Targa Image format.
            /// </summary>
            /// <remarks>Targa Image does not have a Signature of ""TRUEVISION-XFILE"".</remarks>
            OriginalTga = 100,

            /// <summary>
            /// New Targa Image format
            /// </summary>
            /// <remarks>Targa Image has a TargaFooter with a Signature of ""TRUEVISION-XFILE"".</remarks>
            NewTga = 200
        }


        /// <summary>
        /// Indicates the type of color map, if any, included with the image file. 
        /// </summary>
        public enum ColorMapTypes : byte
        {
            /// <summary>
            /// No color map was included in the file.
            /// </summary>
            NoColorMap = 0,

            /// <summary>
            /// Color map was included in the file.
            /// </summary>
            ColorMapIncluded = 1
        }


        /// <summary>
        /// The type of image read from the file.
        /// </summary>
        public enum ImageType : byte
        {
            /// <summary>
            /// No image data was found in file.
            /// </summary>
            NoImageData = 0,

            /// <summary>
            /// Image is an uncompressed, indexed color-mapped image.
            /// </summary>
            UncompressedColorMapped = 1,

            /// <summary>
            /// Image is an uncompressed, RGB image.
            /// </summary>
            UncompressedTrueColor = 2,

            /// <summary>
            /// Image is an uncompressed, Greyscale image.
            /// </summary>
            UncompressedBlackAndWhite = 3,

            /// <summary>
            /// Image is a compressed, indexed color-mapped image.
            /// </summary>
            RunLengthEncodedColorMapped = 9,

            /// <summary>
            /// Image is a compressed, RGB image.
            /// </summary>
            RunLengthEncodedTrueColor = 10,

            /// <summary>
            /// Image is a compressed, Greyscale image.
            /// </summary>
            RunLengthEncodedBlackAndWhite = 11
        }


        /// <summary>
        /// The top-to-bottom ordering in which pixel data is transferred from the file to the screen.
        /// </summary>
        public enum VerticalTransferOrder
        {
            /// <summary>
            /// Unknown transfer order.
            /// </summary>
            Unknown = -1,

            /// <summary>
            /// Transfer order of pixels is from the bottom to top.
            /// </summary>
            Bottom = 0,

            /// <summary>
            /// Transfer order of pixels is from the top to bottom.
            /// </summary>
            Top = 1
        }


        /// <summary>
        /// The left-to-right ordering in which pixel data is transferred from the file to the screen.
        /// </summary>
        public enum HorizontalTransferOrder
        {
            /// <summary>
            /// Unknown transfer order.
            /// </summary>
            Unknown = -1,

            /// <summary>
            /// Transfer order of pixels is from the right to left.
            /// </summary>
            Right = 0,

            /// <summary>
            /// Transfer order of pixels is from the left to right.
            /// </summary>
            Left = 1
        }


        /// <summary>
        /// Screen destination of first pixel based on the VerticalTransferOrder and HorizontalTransferOrder.
        /// </summary>
        public enum FirstPixelDestination
        {
            /// <summary>
            /// Unknown first pixel destination.
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// First pixel destination is the top-left corner of the image.
            /// </summary>
            TopLeft = 1,

            /// <summary>
            /// First pixel destination is the top-right corner of the image.
            /// </summary>
            TopRight = 2,

            /// <summary>
            /// First pixel destination is the bottom-left corner of the image.
            /// </summary>
            BottomLeft = 3,

            /// <summary>
            /// First pixel destination is the bottom-right corner of the image.
            /// </summary>
            BottomRight = 4
        }


        /// <summary>
        /// The RLE packet type used in a RLE compressed image.
        /// </summary>
        public enum RlePacketType
        {
            /// <summary>
            /// A raw RLE packet type.
            /// </summary>
            Raw = 0,

            /// <summary>
            /// A run-length RLE packet type.
            /// </summary>
            RunLength = 1
        }


        private Bitmap bmpTargaImage;
        public byte[] ImageData;
        public ColorPalette Palette;
        private GCHandle imageByteHandle;
        private readonly List<List<byte>> rows = new List<List<byte>>();
        private List<byte> row = new List<byte>();


        // Track whether Dispose has been called.
        private bool disposed;


        /// <summary>
        /// Creates a new instance of the TargaImage object.
        /// </summary>
        public TargaImage(TargaHeader prevHeader = null)
        {
            Footer = new TargaFooter();
            Header = prevHeader ?? new TargaHeader();
            ExtensionArea = new TargaExtensionArea();
            bmpTargaImage = null;
            Thumbnail = null;
        }


        /// <summary>
        /// Gets a TargaHeader object that holds the Targa Header information of the loaded file.
        /// </summary>
        public TargaHeader Header { get; private set; }


        /// <summary>
        /// Gets a TargaExtensionArea object that holds the Targa Extension Area information of the loaded file.
        /// </summary>
        public TargaExtensionArea ExtensionArea { get; private set; }


        /// <summary>
        /// Gets a TargaExtensionArea object that holds the Targa Footer information of the loaded file.
        /// </summary>
        public TargaFooter Footer { get; private set; }


        /// <summary>
        /// Gets the Targa format of the loaded file.
        /// </summary>
        public TgaFormat Format { get; private set; } = TgaFormat.Unknown;


        /// <summary>
        /// Gets a Bitmap representation of the loaded file.
        /// </summary>
        /*public Bitmap Image
        {
            get { return this.bmpTargaImage; }
        }*/

        /// <summary>
        /// Gets the thumbnail of the loaded file if there is one in the file.
        /// </summary>
        public Bitmap Thumbnail { get; private set; }

        /// <summary>
        /// Gets the full path and filename of the loaded file.
        /// </summary>
        public string FileName { get; private set; } = string.Empty;


        /// <summary>
        /// Gets the byte offset between the beginning of one scan line and the next. Used when loading the image into the Image Bitmap.
        /// </summary>
        /// <remarks>
        /// The memory allocated for Microsoft Bitmaps must be aligned on a 32bit boundary.
        /// The stride refers to the number of bytes allocated for one scanline of the bitmap.
        /// </remarks>
        public int Stride { get; private set; }


        /// <summary>
        /// Gets the number of bytes used to pad each scan line to meet the Stride value. Used when loading the image into the Image Bitmap.
        /// </summary>
        /// <remarks>
        /// The memory allocated for Microsoft Bitmaps must be aligned on a 32bit boundary.
        /// The stride refers to the number of bytes allocated for one scanline of the bitmap.
        /// In your loop, you copy the pixels one scanline at a time and take into 
        /// consideration the amount of padding that occurs due to memory alignment.
        /// </remarks>
        public int Padding { get; private set; }


        // Use C# destructor syntax for finalization code.
        // This destructor will run only if the Dispose method 
        // does not get called.
        // It gives your base class the opportunity to finalize.
        // Do not provide destructors in types derived from this class.
        /// <summary>
        /// TargaImage deconstructor.
        /// </summary>
        ~TargaImage()
        {
            // Do not re-create Dispose clean-up code here.
            // Calling Dispose(false) is optimal in terms of
            // readability and maintainability.
            Dispose(false);
        }

        /// <summary>
        /// Creates a new instance of the TargaImage object with strFileName as the image loaded.
        /// </summary>
        public TargaImage(string strFileName)
            : this()
        {
            // make sure we have a .tga file
            if (Path.GetExtension(strFileName)?.ToLower() == ".tga")
            {
                // make sure the file exists
                if (File.Exists(strFileName))
                {
                    FileName = strFileName;

                    // load the file as an array of bytes
                    var filebytes = File.ReadAllBytes(FileName);
                    if (filebytes.Length > 0)
                        LoadFromStream(filebytes);
                    else
                        throw new Exception(@"Error loading file, could not read file from disk.");

                }
                else
                    throw new Exception(@"Error loading file, could not find file '" + strFileName + "' on disk.");

            }
            else
                throw new Exception(@"Error loading file, file '" + strFileName + "' must have an extension of '.tga'.");


        }

        /// <summary>
        /// Creates TGA image from stream.
        /// </summary>
        /// <param name="stream">Stream containing image.</param>
        /// <param name="prevHeader">TargaHeader if previously loaded.</param>
        public TargaImage(Stream stream, TargaHeader prevHeader = null) : this(prevHeader)
        {
            var filebytes = new byte[stream.Length];
            stream.Read(filebytes, 0, (int)stream.Length);
            LoadFromStream(filebytes);
        }

        private void LoadFromStream(byte[] filebytes)
        {
            // create a seekable memory stream of the file bytes
            using (var filestream = new MemoryStream(filebytes))
            {
                if (filestream.Length > 0 && filestream.CanSeek)
                {
                    // create a BinaryReader used to read the Targa file
                    BinaryReader binReader;
                    using (binReader = new BinaryReader(filestream))
                    {
                        LoadTgaFooterInfo(binReader);
                        if (Header.ImageType == ImageType.NoImageData)
                            LoadTgaHeaderInfo(binReader, Header);

                        LoadTgaExtensionArea(binReader);
                        LoadTgaImage(binReader);
                    }
                }
                else
                    throw new Exception(@"Error loading file, could not read file from disk.");
            }
        }

        /// <summary>
        /// Loads the Targa Footer information from the file.
        /// </summary>
        /// <param name="binReader">A BinaryReader that points the loaded file byte stream.</param>
        private void LoadTgaFooterInfo(BinaryReader binReader)
        {

            if (binReader != null && binReader.BaseStream.Length > 0 && binReader.BaseStream.CanSeek)
            {

                try
                {
                    // set the cursor at the beginning of the signature string.
                    binReader.BaseStream.Seek((TargaConstants.FooterSignatureOffsetFromEnd * -1), SeekOrigin.End);

                    // read the signature bytes and convert to ascii string
                    var signature = Encoding.ASCII.GetString(binReader.ReadBytes(TargaConstants.FooterSignatureByteLength)).TrimEnd('\0');

                    // do we have a proper signature
                    if (string.CompareOrdinal(signature, TargaConstants.TargaFooterAsciiSignature) == 0)
                    {
                        // this is a NEW targa file.
                        // create the footer
                        Format = TgaFormat.NewTga;

                        // set cursor to beginning of footer info
                        binReader.BaseStream.Seek((TargaConstants.FooterByteLength * -1), SeekOrigin.End);

                        // read the Extension Area Offset value
                        var extOffset = binReader.ReadInt32();

                        // read the Developer Directory Offset value
                        var devDirOff = binReader.ReadInt32();

                        // skip the signature we have already read it.
                        binReader.ReadBytes(TargaConstants.FooterSignatureByteLength);

                        // read the reserved character
                        var resChar = Encoding.ASCII.GetString(binReader.ReadBytes(TargaConstants.FooterReservedCharByteLength)).TrimEnd('\0');

                        // set all values to our TargaFooter class
                        Footer.SetExtensionAreaOffset(extOffset);
                        Footer.SetDeveloperDirectoryOffset(devDirOff);
                        Footer.SetSignature(signature);
                        Footer.SetReservedCharacter(resChar);
                    }
                    else
                    {
                        // this is not an ORIGINAL targa file.
                        Format = TgaFormat.OriginalTga;
                    }
                }
                catch (Exception)
                {
                    // clear all 
                    ClearAll();
                    throw;
                }
            }
            else
            {
                ClearAll();
                throw new Exception(@"Error loading file, could not read file from disk.");
            }


        }

        /// <summary>
        /// Loads the Targa Header information from the file.
        /// </summary>
        /// <param name="binReader">A BinaryReader that points the loaded file byte stream.</param>
        /// <param name="objTargaHeader"></param>
        public static void LoadTgaHeaderInfo(BinaryReader binReader, TargaHeader objTargaHeader)
        {

            if (binReader != null && binReader.BaseStream.Length > 0 && binReader.BaseStream.CanSeek)
            {
                // set the cursor at the beginning of the file.
                binReader.BaseStream.Seek(0, SeekOrigin.Begin);

                // read the header properties from the file
                objTargaHeader.SetImageIdLength(binReader.ReadByte());
                objTargaHeader.SetColorMapType((ColorMapTypes)binReader.ReadByte());
                objTargaHeader.SetImageType((ImageType)binReader.ReadByte());

                objTargaHeader.SetColorMapFirstEntryIndex(binReader.ReadInt16());
                objTargaHeader.SetColorMapLength(binReader.ReadInt16());
                objTargaHeader.SetColorMapEntrySize(binReader.ReadByte());

                objTargaHeader.SetXOrigin(binReader.ReadInt16());
                objTargaHeader.SetYOrigin(binReader.ReadInt16());
                objTargaHeader.SetWidth(binReader.ReadInt16());
                objTargaHeader.SetHeight(binReader.ReadInt16());

                var pixeldepth = binReader.ReadByte();
                switch (pixeldepth)
                {
                    case 8:
                    case 16:
                    case 24:
                    case 32:
                        objTargaHeader.SetPixelDepth(pixeldepth);
                        break;

                    default:
                        throw new Exception("Targa Image only supports 8, 16, 24, or 32 bit pixel depths.");
                }


                var imageDescriptor = binReader.ReadByte();
                objTargaHeader.SetAttributeBits((byte)Utilities.GetBits(imageDescriptor, 0, 4));

                objTargaHeader.SetVerticalTransferOrder((VerticalTransferOrder)Utilities.GetBits(imageDescriptor, 5, 1));
                objTargaHeader.SetHorizontalTransferOrder((HorizontalTransferOrder)Utilities.GetBits(imageDescriptor, 4, 1));

                // load ImageID value if any
                if (objTargaHeader.ImageIdLength > 0)
                {
                    var imageIdValueBytes = binReader.ReadBytes(objTargaHeader.ImageIdLength);
                    objTargaHeader.SetImageIdValue(Encoding.ASCII.GetString(imageIdValueBytes).TrimEnd('\0'));
                }


                // load color map if it's included and/or needed
                // Only needed for UNCOMPRESSED_COLOR_MAPPED and RUN_LENGTH_ENCODED_COLOR_MAPPED
                // image types. If color map is included for other file types we can ignore it.
                if (objTargaHeader.ColorMapType == ColorMapTypes.ColorMapIncluded)
                {
                    if (objTargaHeader.ImageType != ImageType.UncompressedColorMapped &&
                        objTargaHeader.ImageType != ImageType.RunLengthEncodedColorMapped)
                        return;

                    if (objTargaHeader.ColorMapLength > 0)
                    {
                        for (var i = 0; i < objTargaHeader.ColorMapLength; i++)
                        {
                            int a;
                            int r;
                            int g;
                            int b;

                            // load each color map entry based on the ColorMapEntrySize value
                            switch (objTargaHeader.ColorMapEntrySize)
                            {
                                case 15:
                                    var color15 = binReader.ReadBytes(2);
                                    // remember that the bytes are stored in reverse oreder
                                    objTargaHeader.ColorMap.Add(Utilities.GetColorFrom2Bytes(color15[1], color15[0]));
                                    break;
                                case 16:
                                    var color16 = binReader.ReadBytes(2);
                                    // remember that the bytes are stored in reverse oreder
                                    objTargaHeader.ColorMap.Add(Utilities.GetColorFrom2Bytes(color16[1], color16[0]));
                                    break;
                                case 24:
                                    b = Convert.ToInt32(binReader.ReadByte());
                                    g = Convert.ToInt32(binReader.ReadByte());
                                    r = Convert.ToInt32(binReader.ReadByte());
                                    objTargaHeader.ColorMap.Add(Color.FromArgb(r, g, b));
                                    break;
                                case 32:
                                    a = Convert.ToInt32(binReader.ReadByte());
                                    b = Convert.ToInt32(binReader.ReadByte());
                                    g = Convert.ToInt32(binReader.ReadByte());
                                    r = Convert.ToInt32(binReader.ReadByte());
                                    objTargaHeader.ColorMap.Add(Color.FromArgb(a, r, g, b));
                                    break;
                                default:
                                    throw new Exception("TargaImage only supports ColorMap Entry Sizes of 15, 16, 24 or 32 bits.");

                            }


                        }
                    }
                    else
                    {
                        throw new Exception("Image Type requires a Color Map and Color Map Length is zero.");
                    }


                }
                else
                {
                    if (objTargaHeader.ImageType == ImageType.UncompressedColorMapped ||
                        objTargaHeader.ImageType == ImageType.RunLengthEncodedColorMapped)
                    {
                        throw new Exception("Image Type requires a Color Map and there was not a Color Map included in the file.");
                    }
                }


            }
            else
            {
                throw new Exception(@"Error loading file, could not read file from disk.");
            }
        }


        /// <summary>
        /// Loads the Targa Extension Area from the file, if it exists.
        /// </summary>
        /// <param name="binReader">A BinaryReader that points the loaded file byte stream.</param>
        private void LoadTgaExtensionArea(BinaryReader binReader)
        {

            if (binReader != null && binReader.BaseStream.Length > 0 && binReader.BaseStream.CanSeek)
            {
                // is there an Extension Area in file
                if (Footer.ExtensionAreaOffset > 0)
                {
                    try
                    {
                        // set the cursor at the beginning of the Extension Area using ExtensionAreaOffset.
                        binReader.BaseStream.Seek(Footer.ExtensionAreaOffset, SeekOrigin.Begin);

                        // load the extension area fields from the file

                        ExtensionArea.SetExtensionSize(binReader.ReadInt16());
                        ExtensionArea.SetAuthorName(Encoding.ASCII.GetString(binReader.ReadBytes(TargaConstants.ExtensionAreaAuthorNameByteLength)).TrimEnd('\0'));
                        ExtensionArea.SetAuthorComments(Encoding.ASCII.GetString(binReader.ReadBytes(TargaConstants.ExtensionAreaAuthorCommentsByteLength)).TrimEnd('\0'));


                        // get the date/time stamp of the file
                        var iMonth = binReader.ReadInt16();
                        var iDay = binReader.ReadInt16();
                        var iYear = binReader.ReadInt16();
                        var iHour = binReader.ReadInt16();
                        var iMinute = binReader.ReadInt16();
                        var iSecond = binReader.ReadInt16();
                        DateTime dtstamp;
                        var strStamp = iMonth + @"/" + iDay + @"/" + iYear + @" ";
                        strStamp += iHour + @":" + iMinute + @":" + iSecond;
                        if (DateTime.TryParse(strStamp, out dtstamp))
                            ExtensionArea.SetDateTimeStamp(dtstamp);


                        ExtensionArea.SetJobName(Encoding.ASCII.GetString(binReader.ReadBytes(TargaConstants.ExtensionAreaJobNameByteLength)).TrimEnd('\0'));


                        // get the job time of the file
                        iHour = binReader.ReadInt16();
                        iMinute = binReader.ReadInt16();
                        iSecond = binReader.ReadInt16();
                        var ts = new TimeSpan(iHour, iMinute, iSecond);
                        ExtensionArea.SetJobTime(ts);


                        ExtensionArea.SetSoftwareId(Encoding.ASCII.GetString(binReader.ReadBytes(TargaConstants.ExtensionAreaSoftwareIdByteLength)).TrimEnd('\0'));


                        // get the version number and letter from file
                        var iVersionNumber = binReader.ReadInt16() / 100.0F;
                        var strVersionLetter = Encoding.ASCII.GetString(binReader.ReadBytes(TargaConstants.ExtensionAreaSoftwareVersionLetterByteLength)).TrimEnd('\0');


                        ExtensionArea.SetSoftwareId(iVersionNumber.ToString(@"F2") + strVersionLetter);


                        // get the color key of the file
                        int a = binReader.ReadByte();
                        int r = binReader.ReadByte();
                        int b = binReader.ReadByte();
                        int g = binReader.ReadByte();
                        ExtensionArea.SetKeyColor(Color.FromArgb(a, r, g, b));


                        ExtensionArea.SetPixelAspectRatioNumerator(binReader.ReadInt16());
                        ExtensionArea.SetPixelAspectRatioDenominator(binReader.ReadInt16());
                        ExtensionArea.SetGammaNumerator(binReader.ReadInt16());
                        ExtensionArea.SetGammaDenominator(binReader.ReadInt16());
                        ExtensionArea.SetColorCorrectionOffset(binReader.ReadInt32());
                        ExtensionArea.SetPostageStampOffset(binReader.ReadInt32());
                        ExtensionArea.SetScanLineOffset(binReader.ReadInt32());
                        ExtensionArea.SetAttributesType(binReader.ReadByte());


                        // load Scan Line Table from file if any
                        if (ExtensionArea.ScanLineOffset > 0)
                        {
                            binReader.BaseStream.Seek(ExtensionArea.ScanLineOffset, SeekOrigin.Begin);
                            for (var i = 0; i < Header.Height; i++)
                            {
                                ExtensionArea.ScanLineTable.Add(binReader.ReadInt32());
                            }
                        }


                        // load Color Correction Table from file if any
                        if (ExtensionArea.ColorCorrectionOffset > 0)
                        {
                            binReader.BaseStream.Seek(ExtensionArea.ColorCorrectionOffset, SeekOrigin.Begin);
                            for (var i = 0; i < TargaConstants.ExtensionAreaColorCorrectionTableValueLength; i++)
                            {
                                a = binReader.ReadInt16();
                                r = binReader.ReadInt16();
                                b = binReader.ReadInt16();
                                g = binReader.ReadInt16();
                                ExtensionArea.ColorCorrectionTable.Add(Color.FromArgb(a, r, g, b));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        ClearAll();
                        throw;
                    }
                }
            }
            else
            {
                ClearAll();
                throw new Exception(@"Error loading file, could not read file from disk.");
            }
        }

        /// <summary>
        /// Reads the image data bytes from the file. Handles Uncompressed and RLE Compressed image data. 
        /// Uses FirstPixelDestination to properly align the image.
        /// </summary>
        /// <param name="binReader">A BinaryReader that points the loaded file byte stream.</param>
        /// <returns>An array of bytes representing the image data in the proper alignment.</returns>
        private byte[] LoadImageBytes(BinaryReader binReader)
        {

            // read the image data into a byte array
            // take into account stride has to be a multiple of 4
            // use padding to make sure multiple of 4    

            byte[] data;
            if (binReader?.BaseStream != null && binReader.BaseStream.Length > 0 && binReader.BaseStream.CanSeek)
            {
                if (Header.ImageDataOffset > 0)
                {
                    // padding bytes
                    var padding = new byte[Padding];
                    MemoryStream msData;

                    // seek to the beginning of the image data using the ImageDataOffset value
                    binReader.BaseStream.Seek(Header.ImageDataOffset, SeekOrigin.Begin);


                    // get the size in bytes of each row in the image
                    var intImageRowByteSize = Header.Width * Header.BytesPerPixel;

                    // get the size in bytes of the whole image
                    var intImageByteSize = intImageRowByteSize * Header.Height;

                    // is this a RLE compressed image type
                    if (Header.ImageType == ImageType.RunLengthEncodedBlackAndWhite ||
                       Header.ImageType == ImageType.RunLengthEncodedColorMapped ||
                       Header.ImageType == ImageType.RunLengthEncodedTrueColor)
                    {

                        #region COMPRESSED

                        // used to keep track of bytes read
                        var intImageBytesRead = 0;
                        var intImageRowBytesRead = 0;

                        // keep reading until we have the all image bytes
                        while (intImageBytesRead < intImageByteSize)
                        {
                            // get the RLE packet
                            var bRlePacket = binReader.ReadByte();
                            var intRlePacketType = Utilities.GetBits(bRlePacket, 7, 1);
                            var intRlePixelCount = Utilities.GetBits(bRlePacket, 0, 7) + 1;

                            // check the RLE packet type
                            switch ((RlePacketType)intRlePacketType)
                            {
                                case RlePacketType.RunLength:
                                    // get the pixel color data
                                    var bRunLengthPixel = binReader.ReadBytes(Header.BytesPerPixel);

                                    // add the number of pixels specified using the read pixel color
                                    for (var i = 0; i < intRlePixelCount; i++)
                                    {
                                        foreach (var b in bRunLengthPixel)
                                            row.Add(b);

                                        // increment the byte counts
                                        intImageRowBytesRead += bRunLengthPixel.Length;
                                        intImageBytesRead += bRunLengthPixel.Length;

                                        // if we have read a full image row
                                        // add the row to the row list and clear it
                                        // restart row byte count
                                        if (intImageRowBytesRead == intImageRowByteSize)
                                        {
                                            rows.Add(row);
                                            row = new List<byte>();
                                            intImageRowBytesRead = 0;

                                        }
                                    }

                                    break;
                                case RlePacketType.Raw:
                                    // get the number of bytes to read based on the read pixel count
                                    var intBytesToRead = intRlePixelCount * Header.BytesPerPixel;

                                    // read each byte
                                    for (var i = 0; i < intBytesToRead; i++)
                                    {
                                        row.Add(binReader.ReadByte());

                                        // increment the byte counts
                                        intImageBytesRead++;
                                        intImageRowBytesRead++;

                                        // if we have read a full image row
                                        // add the row to the row list and clear it
                                        // restart row byte count
                                        if (intImageRowBytesRead == intImageRowByteSize)
                                        {
                                            rows.Add(row);
                                            row = new List<byte>();
                                            intImageRowBytesRead = 0;
                                        }

                                    }

                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }

                        #endregion

                    }

                    else
                    {
                        #region NON-COMPRESSED

                        // loop through each row in the image
                        for (var i = 0; i < (int)Header.Height; i++)
                        {
                            // loop through each byte in the row
                            for (var j = 0; j < intImageRowByteSize; j++)
                            {
                                // add the byte to the row
                                row.Add(binReader.ReadByte());
                            }

                            // add row to the list of rows
                            rows.Add(row);

                            // create a new row
                            row = new List<byte>();
                        }


                        #endregion
                    }

                    // flag that states whether or not to reverse the location of all rows.
                    var blnRowsReverse = false;

                    // flag that states whether or not to reverse the bytes in each row.
                    var blnEachRowReverse = false;

                    // use FirstPixelDestination to determine the alignment of the 
                    // image data byte
                    switch (Header.FirstPixelDestination)
                    {
                        case FirstPixelDestination.TopLeft:
                            blnEachRowReverse = true;
                            break;

                        case FirstPixelDestination.TopRight:
                            break;

                        case FirstPixelDestination.BottomLeft:
                            blnRowsReverse = true;
                            blnEachRowReverse = true;
                            break;

                        case FirstPixelDestination.BottomRight:
                        case FirstPixelDestination.Unknown:
                            blnRowsReverse = true;

                            break;
                    }

                    // write the bytes from each row into a memory stream and get the 
                    // resulting byte array
                    using (msData = new MemoryStream())
                    {

                        // do we reverse the rows in the row list.
                        if (blnRowsReverse)
                            rows.Reverse();

                        // go through each row
                        foreach (var t in rows)
                        {
                            // do we reverse the bytes in the row
                            if (blnEachRowReverse)
                                t.Reverse();

                            // get the byte array for the row
                            var brow = t.ToArray();

                            // write the row bytes and padding bytes to the memory streem
                            msData.Write(brow, 0, brow.Length);
                            msData.Write(padding, 0, padding.Length);
                        }
                        // get the image byte array
                        data = msData.ToArray();



                    }

                }
                else
                {
                    ClearAll();
                    throw new Exception(@"Error loading file, No image data in file.");
                }
            }
            else
            {
                ClearAll();
                throw new Exception(@"Error loading file, could not read file from disk.");
            }

            // return the image byte array
            return data;

        }

        /// <summary>
        /// Reads the image data bytes from the file and loads them into the Image Bitmap object.
        /// Also loads the color map, if any, into the Image Bitmap.
        /// </summary>
        /// <param name="binReader">A BinaryReader that points the loaded file byte stream.</param>
        private void LoadTgaImage(BinaryReader binReader)
        {
            //**************  NOTE  *******************
            // The memory allocated for Microsoft Bitmaps must be aligned on a 32bit boundary.
            // The stride refers to the number of bytes allocated for one scanline of the bitmap.
            // In your loop, you copy the pixels one scanline at a time and take into
            // consideration the amount of padding that occurs due to memory alignment.
            // calculate the stride, in bytes, of the image (32bit aligned width of each image row)
            Stride = ((Header.Width * Header.PixelDepth + 31) & ~31) >> 3; // width in bytes

            // calculate the padding, in bytes, of the image 
            // number of bytes to add to make each row a 32bit aligned row
            // padding in bytes
            Padding = Stride - (((Header.Width * Header.PixelDepth) + 7) / 8);

            // get the image data bytes
            ImageData = LoadImageBytes(binReader);

            // since the Bitmap constructor requires a poiter to an array of image bytes
            // we have to pin down the memory used by the byte array and use the pointer 
            // of this pinned memory to create the Bitmap.
            // This tells the Garbage Collector to leave the memory alone and DO NOT touch it.
            imageByteHandle = GCHandle.Alloc(ImageData, GCHandleType.Pinned);

            // make sure we don't have a phantom Bitmap
            bmpTargaImage?.Dispose();

            // make sure we don't have a phantom Thumbnail
            Thumbnail?.Dispose();


            // get the Pixel format to use with the Bitmap object
            var pf = GetPixelFormat();


            // create a Bitmap object using the image Width, Height,
            // Stride, PixelFormat and the pointer to the pinned byte array.
            bmpTargaImage = new Bitmap(Header.Width,
                                            Header.Height,
                                            Stride,
                                            pf,
                                            imageByteHandle.AddrOfPinnedObject());

            Palette = bmpTargaImage.Palette;
            imageByteHandle.Free();


            LoadThumbnail(binReader);



            // load the color map into the Bitmap, if it exists
            if (Header.ColorMap.Count > 0)
            {
                // loop trough each color in the loaded file's color map
                for (var i = 0; i < Header.ColorMap.Count; i++)
                {
                    // is the AttributesType 0 or 1 bit
                    if (ExtensionArea.AttributesType == 0 ||
                        ExtensionArea.AttributesType == 1)
                        // use 255 for alpha ( 255 = opaque/visible ) so we can see the image
                        Palette.Entries[i] = Color.FromArgb(255, Header.ColorMap[i].R, Header.ColorMap[i].G, Header.ColorMap[i].B);

                    else
                        // use whatever value is there
                        Palette.Entries[i] = Header.ColorMap[i];

                }
            }
            else
            { // no color map


                // check to see if this is a Black and White (Greyscale)
                if (Header.PixelDepth == 8 && (Header.ImageType == ImageType.UncompressedBlackAndWhite ||
                    Header.ImageType == ImageType.RunLengthEncodedBlackAndWhite))
                {
                    // create the Greyscale palette
                    for (var i = 0; i < 256; i++)
                    {
                        Palette.Entries[i] = Color.FromArgb(i, i, i);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the PixelFormat to be used by the Image based on the Targa file's attributes
        /// </summary>
        /// <returns></returns>
        private PixelFormat GetPixelFormat()
        {

            var pfTargaPixelFormat = PixelFormat.Undefined;

            // first off what is our Pixel Depth (bits per pixel)
            switch (Header.PixelDepth)
            {
                case 8:
                    pfTargaPixelFormat = PixelFormat.Format8bppIndexed;
                    break;

                case 16:
                    //PixelFormat.Format16bppArgb1555
                    //PixelFormat.Format16bppRgb555
                    if (Format == TgaFormat.NewTga)
                    {
                        switch (ExtensionArea.AttributesType)
                        {
                            case 0:
                            case 1:
                            case 2: // no alpha data
                                pfTargaPixelFormat = PixelFormat.Format16bppRgb555;
                                break;

                            case 3: // useful alpha data
                                pfTargaPixelFormat = PixelFormat.Format16bppArgb1555;
                                break;
                        }
                    }
                    else
                    {
                        pfTargaPixelFormat = PixelFormat.Format16bppRgb555;
                    }

                    break;

                case 24:
                    pfTargaPixelFormat = PixelFormat.Format24bppRgb;
                    break;

                case 32:
                    //PixelFormat.Format32bppArgb
                    //PixelFormat.Format32bppPArgb
                    //PixelFormat.Format32bppRgb
                    if (Format == TgaFormat.NewTga)
                    {
                        switch (ExtensionArea.AttributesType)
                        {

                            case 1:
                            case 2: // no alpha data
                                pfTargaPixelFormat = PixelFormat.Format32bppRgb;
                                break;

                            case 0:
                            case 3: // useful alpha data
                                pfTargaPixelFormat = PixelFormat.Format32bppArgb;
                                break;

                            case 4: // premultiplied alpha data
                                pfTargaPixelFormat = PixelFormat.Format32bppPArgb;
                                break;

                        }
                    }
                    else
                    {
                        pfTargaPixelFormat = PixelFormat.Format32bppRgb;
                    }



                    break;

            }


            return pfTargaPixelFormat;
        }


        /// <summary>
        /// Loads the thumbnail of the loaded image file, if any.
        /// </summary>
        /// <param name="binReader">A BinaryReader that points the loaded file byte stream.</param>
        private void LoadThumbnail(BinaryReader binReader)
        {

            // read the Thumbnail image data into a byte array
            // take into account stride has to be a multiple of 4
            // use padding to make sure multiple of 4    

            if (binReader?.BaseStream != null && binReader.BaseStream.Length > 0 && binReader.BaseStream.CanSeek)
            {
                if (ExtensionArea.PostageStampOffset > 0)
                {

                    // seek to the beginning of the image data using the ImageDataOffset value
                    binReader.BaseStream.Seek(ExtensionArea.PostageStampOffset, SeekOrigin.Begin);

                    int iWidth = binReader.ReadByte();
                    int iHeight = binReader.ReadByte();

                    var iStride = ((iWidth * Header.PixelDepth + 31) & ~31) >> 3; // width in bytes
                    var iPadding = iStride - (((iWidth * Header.PixelDepth) + 7) / 8);

                    var objRows = new List<List<byte>>();
                    var objRow = new List<byte>();




                    var padding = new byte[iPadding];
                    MemoryStream msData;
                    var blnRowsReverse = false;


                    using (msData = new MemoryStream())
                    {
                        // get the size in bytes of each row in the image
                        var intImageRowByteSize = iWidth * (Header.PixelDepth / 8);

                        // thumbnails are never compressed
                        for (var i = 0; i < iHeight; i++)
                        {
                            for (var j = 0; j < intImageRowByteSize; j++)
                            {
                                objRow.Add(binReader.ReadByte());
                            }
                            objRows.Add(objRow);
                            objRow = new List<byte>();
                        }

                        switch (Header.FirstPixelDestination)
                        {
                            case FirstPixelDestination.TopLeft:
                                break;

                            case FirstPixelDestination.TopRight:
                                break;

                            case FirstPixelDestination.BottomLeft:
                                break;

                            case FirstPixelDestination.BottomRight:
                            case FirstPixelDestination.Unknown:
                                blnRowsReverse = true;
                                break;
                        }

                        if (blnRowsReverse)
                            objRows.Reverse();

                        foreach (var t in objRows)
                        {
                            var brow = t.ToArray();
                            msData.Write(brow, 0, brow.Length);
                            msData.Write(padding, 0, padding.Length);
                        }
                        msData.ToArray();
                    }
                }
                else
                {
                    if (Thumbnail != null)
                    {
                        Thumbnail.Dispose();
                        Thumbnail = null;
                    }
                }
            }
            else
            {
                if (Thumbnail != null)
                {
                    Thumbnail.Dispose();
                    Thumbnail = null;
                }
            }

        }

        /// <summary>
        /// Clears out all objects and resources.
        /// </summary>
        private void ClearAll()
        {
            if (bmpTargaImage != null)
            {
                bmpTargaImage.Dispose();
                bmpTargaImage = null;
            }
            if (imageByteHandle.IsAllocated)
                imageByteHandle.Free();

            Header = new TargaHeader();
            ExtensionArea = new TargaExtensionArea();
            Footer = new TargaFooter();
            Format = TgaFormat.Unknown;
            Stride = 0;
            Padding = 0;
            rows.Clear();
            row.Clear();
            FileName = string.Empty;

        }

        #region IDisposable Members

        /// <summary>
        /// Disposes all resources used by this instance of the TargaImage class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            // Take yourself off the Finalization queue 
            // to prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);

        }


        /// <summary>
        /// Dispose(bool disposing) executes in two distinct scenarios.
        /// If disposing equals true, the method has been called directly
        /// or indirectly by a user's code. Managed and unmanaged resources
        /// can be disposed.
        /// If disposing equals false, the method has been called by the 
        /// runtime from inside the finalizer and you should not reference 
        /// other objects. Only unmanaged resources can be disposed.
        /// </summary>
        /// <param name="disposing">If true dispose all resources, else dispose only release unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!disposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources.
                if (disposing)
                {
                    // Dispose managed resources.
                    bmpTargaImage?.Dispose();

                    Thumbnail?.Dispose();

                    if (imageByteHandle.IsAllocated)
                    {
                        imageByteHandle.Free();
                    }

                }
                // Release unmanaged resources. If disposing is false, 
                // only the following code is executed.
                // ** release unmanged resources here **

                // Note that this is not thread safe.
                // Another thread could start disposing the object
                // after the managed resources are disposed,
                // but before the disposed flag is set to true.
                // If thread safety is necessary, it must be
                // implemented by the client.

            }
            disposed = true;
        }

        #endregion
    }


    /// <summary>
    /// This class holds all of the header properties of a Targa image. 
    /// This includes the TGA File Header section the ImageID and the Color Map.
    /// </summary>
    internal class TargaHeader
    {
        /// <summary>
        /// Gets the number of bytes contained the ImageIDValue property. The maximum
        /// number of characters is 255. A value of zero indicates that no ImageIDValue is included with the
        /// image.
        /// </summary>
        public byte ImageIdLength { get; private set; }

        /// <summary>
        /// Sets the ImageIDLength property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="imageIdLength">The Image ID Length value read from the file.</param>
        protected internal void SetImageIdLength(byte imageIdLength)
        {
            ImageIdLength = imageIdLength;
        }

        /// <summary>
        /// Gets the type of color map (if any) included with the image. There are currently 2
        /// defined values for this field:
        /// NO_COLOR_MAP - indicates that no color-map data is included with this image.
        /// COLOR_MAP_INCLUDED - indicates that a color-map is included with this image.
        /// </summary>
        public TargaImage.ColorMapTypes ColorMapType { get; private set; } = TargaImage.ColorMapTypes.NoColorMap;

        /// <summary>
        /// Sets the ColorMapType property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="colorMapType">One of the ColorMapType enumeration values.</param>
        protected internal void SetColorMapType(TargaImage.ColorMapTypes colorMapType)
        {
            ColorMapType = colorMapType;
        }

        /// <summary>
        /// Gets one of the ImageType enumeration values indicating the type of Targa image read from the file.
        /// </summary>
        public TargaImage.ImageType ImageType { get; private set; } = TargaImage.ImageType.NoImageData;

        /// <summary>
        /// Sets the ImageType property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="imageType">One of the ImageType enumeration values.</param>
        protected internal void SetImageType(TargaImage.ImageType imageType)
        {
            ImageType = imageType;
        }

        /// <summary>
        /// Gets the index of the first color map entry. ColorMapFirstEntryIndex refers to the starting entry in loading the color map.
        /// </summary>
        public short ColorMapFirstEntryIndex { get; private set; }

        /// <summary>
        /// Sets the ColorMapFirstEntryIndex property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="colorMapFirstEntryIndex">The First Entry Index value read from the file.</param>
        protected internal void SetColorMapFirstEntryIndex(short colorMapFirstEntryIndex)
        {
            ColorMapFirstEntryIndex = colorMapFirstEntryIndex;
        }

        /// <summary>
        /// Gets total number of color map entries included.
        /// </summary>
        public short ColorMapLength { get; private set; }

        /// <summary>
        /// Sets the ColorMapLength property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="colorMapLength">The Color Map Length value read from the file.</param>
        protected internal void SetColorMapLength(short colorMapLength)
        {
            ColorMapLength = colorMapLength;
        }

        /// <summary>
        /// Gets the number of bits per entry in the Color Map. Typically 15, 16, 24 or 32-bit values are used.
        /// </summary>
        public byte ColorMapEntrySize { get; private set; }

        /// <summary>
        /// Sets the ColorMapEntrySize property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="colorMapEntrySize">The Color Map Entry Size value read from the file.</param>
        protected internal void SetColorMapEntrySize(byte colorMapEntrySize)
        {
            ColorMapEntrySize = colorMapEntrySize;
        }

        /// <summary>
        /// Gets the absolute horizontal coordinate for the lower
        /// left corner of the image as it is positioned on a display device having
        /// an origin at the lower left of the screen (e.g., the TARGA series).
        /// </summary>
        public short XOrigin { get; private set; }

        /// <summary>
        /// Sets the XOrigin property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="xOrigin">The X Origin value read from the file.</param>
        protected internal void SetXOrigin(short xOrigin)
        {
            XOrigin = xOrigin;
        }

        /// <summary>
        /// These bytes specify the absolute vertical coordinate for the lower left
        /// corner of the image as it is positioned on a display device having an
        /// origin at the lower left of the screen (e.g., the TARGA series).
        /// </summary>
        public short YOrigin { get; private set; }

        /// <summary>
        /// Sets the YOrigin property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="yOrigin">The Y Origin value read from the file.</param>
        protected internal void SetYOrigin(short yOrigin)
        {
            YOrigin = yOrigin;
        }

        /// <summary>
        /// Gets the width of the image in pixels.
        /// </summary>
        public short Width { get; private set; }

        /// <summary>
        /// Sets the Width property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="width">The Width value read from the file.</param>
        protected internal void SetWidth(short width)
        {
            Width = width;
        }

        /// <summary>
        /// Gets the height of the image in pixels.
        /// </summary>
        public short Height { get; private set; }

        /// <summary>
        /// Sets the Height property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="height">The Height value read from the file.</param>
        protected internal void SetHeight(short height)
        {
            Height = height;
        }

        /// <summary>
        /// Gets the number of bits per pixel. This number includes
        /// the Attribute or Alpha channel bits. Common values are 8, 16, 24 and 32.
        /// </summary>
        public byte PixelDepth { get; private set; }

        /// <summary>
        /// Sets the PixelDepth property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="pixelDepth">The Pixel Depth value read from the file.</param>
        protected internal void SetPixelDepth(byte pixelDepth)
        {
            PixelDepth = pixelDepth;
        }

        /// <summary>
        /// Gets or Sets the ImageDescriptor property. The ImageDescriptor is the byte that holds the 
        /// Image Origin and Attribute Bits values.
        /// Available only to objects in the same assembly as TargaHeader.
        /// </summary>
        protected internal byte ImageDescriptor { get; set; }

        /// <summary>
        /// Gets one of the FirstPixelDestination enumeration values specifying the screen destination of first pixel based on VerticalTransferOrder and HorizontalTransferOrder
        /// </summary>
        public TargaImage.FirstPixelDestination FirstPixelDestination
        {
            get
            {

                if (VerticalTransferOrder == TargaImage.VerticalTransferOrder.Unknown || HorizontalTransferOrder == TargaImage.HorizontalTransferOrder.Unknown)
                    return TargaImage.FirstPixelDestination.Unknown;
                if (VerticalTransferOrder == TargaImage.VerticalTransferOrder.Bottom && HorizontalTransferOrder == TargaImage.HorizontalTransferOrder.Left)
                    return TargaImage.FirstPixelDestination.BottomLeft;
                if (VerticalTransferOrder == TargaImage.VerticalTransferOrder.Bottom && HorizontalTransferOrder == TargaImage.HorizontalTransferOrder.Right)
                    return TargaImage.FirstPixelDestination.BottomRight;
                if (VerticalTransferOrder == TargaImage.VerticalTransferOrder.Top && HorizontalTransferOrder == TargaImage.HorizontalTransferOrder.Left)
                    return TargaImage.FirstPixelDestination.TopLeft;
                return TargaImage.FirstPixelDestination.TopRight;

            }
        }


        /// <summary>
        /// Gets one of the VerticalTransferOrder enumeration values specifying the top-to-bottom ordering in which pixel data is transferred from the file to the screen.
        /// </summary>
        public TargaImage.VerticalTransferOrder VerticalTransferOrder { get; private set; } = TargaImage.VerticalTransferOrder.Unknown;

        /// <summary>
        /// Sets the VerticalTransferOrder property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="eVerticalTransferOrder">One of the VerticalTransferOrder enumeration values.</param>
        protected internal void SetVerticalTransferOrder(TargaImage.VerticalTransferOrder eVerticalTransferOrder)
        {
            VerticalTransferOrder = eVerticalTransferOrder;
        }

        /// <summary>
        /// Gets one of the HorizontalTransferOrder enumeration values specifying the left-to-right ordering in which pixel data is transferred from the file to the screen.
        /// </summary>
        public TargaImage.HorizontalTransferOrder HorizontalTransferOrder { get; private set; } = TargaImage.HorizontalTransferOrder.Unknown;

        /// <summary>
        /// Sets the HorizontalTransferOrder property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="eHorizontalTransferOrder">One of the HorizontalTransferOrder enumeration values.</param>
        protected internal void SetHorizontalTransferOrder(TargaImage.HorizontalTransferOrder eHorizontalTransferOrder)
        {
            HorizontalTransferOrder = eHorizontalTransferOrder;
        }

        /// <summary>
        /// Gets the number of attribute bits per pixel.
        /// </summary>
        public byte AttributeBits { get; private set; }

        /// <summary>
        /// Sets the AttributeBits property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="bAttributeBits">The Attribute Bits value read from the file.</param>
        protected internal void SetAttributeBits(byte bAttributeBits)
        {
            AttributeBits = bAttributeBits;
        }

        /// <summary>
        /// Gets identifying information about the image. 
        /// A value of zero in ImageIDLength indicates that no ImageIDValue is included with the image.
        /// </summary>
        public string ImageIdValue { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the ImageIDValue property, available only to objects in the same assembly as TargaHeader.
        /// </summary>
        /// <param name="strImageIdValue">The Image ID value read from the file.</param>
        protected internal void SetImageIdValue(string strImageIdValue)
        {
            ImageIdValue = strImageIdValue;
        }

        /// <summary>
        /// Gets the Color Map of the image, if any. The Color Map is represented by a list of System.Drawing.Color objects.
        /// </summary>
        public List<Color> ColorMap { get; } = new List<Color>();

        /// <summary>
        /// Gets the offset from the beginning of the file to the Image Data.
        /// </summary>
        public int ImageDataOffset
        {
            get
            {
                // calculate the image data offset

                // start off with the number of bytes holding the header info.
                var intImageDataOffset = TargaImage.TargaConstants.HeaderByteLength;

                // add the Image ID length (could be variable)
                intImageDataOffset += ImageIdLength;

                // determine the number of bytes for each Color Map entry
                var bytes = 0;
                switch (ColorMapEntrySize)
                {
                    case 15:
                        bytes = 2;
                        break;
                    case 16:
                        bytes = 2;
                        break;
                    case 24:
                        bytes = 3;
                        break;
                    case 32:
                        bytes = 4;
                        break;
                }

                // add the length of the color map
                intImageDataOffset += (ColorMapLength * bytes);

                // return result
                return intImageDataOffset;
            }
        }

        /// <summary>
        /// Gets the number of bytes per pixel.
        /// </summary>
        public int BytesPerPixel => PixelDepth / 8;
    }


    /// <summary>
    /// Holds Footer infomation read from the image file.
    /// </summary>
    internal class TargaFooter
    {
        /// <summary>
        /// Gets the offset from the beginning of the file to the start of the Extension Area. 
        /// If the ExtensionAreaOffset is zero, no Extension Area exists in the file.
        /// </summary>
        public int ExtensionAreaOffset { get; private set; }

        /// <summary>
        /// Sets the ExtensionAreaOffset property, available only to objects in the same assembly as TargaFooter.
        /// </summary>
        /// <param name="intExtensionAreaOffset">The Extension Area Offset value read from the file.</param>
        protected internal void SetExtensionAreaOffset(int intExtensionAreaOffset)
        {
            ExtensionAreaOffset = intExtensionAreaOffset;
        }

        /// <summary>
        /// Gets the offset from the beginning of the file to the start of the Developer Area.
        /// If the DeveloperDirectoryOffset is zero, then the Developer Area does not exist
        /// </summary>
        public int DeveloperDirectoryOffset { get; private set; }

        /// <summary>
        /// Sets the DeveloperDirectoryOffset property, available only to objects in the same assembly as TargaFooter.
        /// </summary>
        /// <param name="intDeveloperDirectoryOffset">The Developer Directory Offset value read from the file.</param>
        protected internal void SetDeveloperDirectoryOffset(int intDeveloperDirectoryOffset)
        {
            DeveloperDirectoryOffset = intDeveloperDirectoryOffset;
        }

        /// <summary>
        /// This string is formatted exactly as "TRUEVISION-XFILE" (no quotes). If the
        /// signature is detected, the file is assumed to be a New TGA format and MAY,
        /// therefore, contain the Developer Area and/or the Extension Areas. If the
        /// signature is not found, then the file is assumed to be an Original TGA format.
        /// </summary>
        public string Signature { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the Signature property, available only to objects in the same assembly as TargaFooter.
        /// </summary>
        /// <param name="strSignature">The Signature value read from the file.</param>
        protected internal void SetSignature(string strSignature)
        {
            Signature = strSignature;
        }

        /// <summary>
        /// A New Targa format reserved character "." (period)
        /// </summary>
        public string ReservedCharacter { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the ReservedCharacter property, available only to objects in the same assembly as TargaFooter.
        /// </summary>
        /// <param name="strReservedCharacter">The ReservedCharacter value read from the file.</param>
        protected internal void SetReservedCharacter(string strReservedCharacter)
        {
            ReservedCharacter = strReservedCharacter;
        }
    }


    /// <summary>
    /// This class holds all of the Extension Area properties of the Targa image. If an Extension Area exists in the file.
    /// </summary>
    internal class TargaExtensionArea
    {
        /// <summary>
        /// Gets the number of Bytes in the fixed-length portion of the ExtensionArea. 
        /// For Version 2.0 of the TGA File Format, this number should be set to 495
        /// </summary>
        public int ExtensionSize { get; private set; }

        /// <summary>
        /// Sets the ExtensionSize property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="intExtensionSize">The Extension Size value read from the file.</param>
        protected internal void SetExtensionSize(int intExtensionSize)
        {
            ExtensionSize = intExtensionSize;
        }

        /// <summary>
        /// Gets the name of the person who created the image.
        /// </summary>
        public string AuthorName { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the AuthorName property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="strAuthorName">The Author Name value read from the file.</param>
        protected internal void SetAuthorName(string strAuthorName)
        {
            AuthorName = strAuthorName;
        }

        /// <summary>
        /// Gets the comments from the author who created the image.
        /// </summary>
        public string AuthorComments { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the AuthorComments property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="strAuthorComments">The Author Comments value read from the file.</param>
        protected internal void SetAuthorComments(string strAuthorComments)
        {
            AuthorComments = strAuthorComments;
        }

        /// <summary>
        /// Gets the date and time that the image was saved.
        /// </summary>
        public DateTime DateTimeStamp { get; private set; } = DateTime.Now;

        /// <summary>
        /// Sets the DateTimeStamp property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="dtDateTimeStamp">The Date Time Stamp value read from the file.</param>
        protected internal void SetDateTimeStamp(DateTime dtDateTimeStamp)
        {
            DateTimeStamp = dtDateTimeStamp;
        }

        /// <summary>
        /// Gets the name or id tag which refers to the job with which the image was associated.
        /// </summary>
        public string JobName { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the JobName property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="strJobName">The Job Name value read from the file.</param>
        protected internal void SetJobName(string strJobName)
        {
            JobName = strJobName;
        }

        /// <summary>
        /// Gets the job elapsed time when the image was saved.
        /// </summary>
        public TimeSpan JobTime { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// Sets the JobTime property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="dtJobTime">The Job Time value read from the file.</param>
        protected internal void SetJobTime(TimeSpan dtJobTime)
        {
            JobTime = dtJobTime;
        }

        /// <summary>
        /// Gets the Software ID. Usually used to determine and record with what program a particular image was created.
        /// </summary>
        public string SoftwareId { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the SoftwareID property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="strSoftwareId">The Software ID value read from the file.</param>
        protected internal void SetSoftwareId(string strSoftwareId)
        {
            SoftwareId = strSoftwareId;
        }

        /// <summary>
        /// Gets the version of software defined by the SoftwareID.
        /// </summary>
        public string SoftwareVersion { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the SoftwareVersion property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="strSoftwareVersion">The Software Version value read from the file.</param>
        protected internal void SetSoftwareVersion(string strSoftwareVersion)
        {
            SoftwareVersion = strSoftwareVersion;
        }

        /// <summary>
        /// Gets the key color in effect at the time the image is saved.
        /// The Key Color can be thought of as the "background color" or "transparent color".
        /// </summary>
        public Color KeyColor { get; private set; } = Color.Empty;

        /// <summary>
        /// Sets the KeyColor property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="cKeyColor">The Key Color value read from the file.</param>
        protected internal void SetKeyColor(Color cKeyColor)
        {
            KeyColor = cKeyColor;
        }

        /// <summary>
        /// Gets the Pixel Ratio Numerator.
        /// </summary>
        public int PixelAspectRatioNumerator { get; private set; }

        /// <summary>
        /// Sets the PixelAspectRatioNumerator property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="intPixelAspectRatioNumerator">The Pixel Aspect Ratio Numerator value read from the file.</param>
        protected internal void SetPixelAspectRatioNumerator(int intPixelAspectRatioNumerator)
        {
            PixelAspectRatioNumerator = intPixelAspectRatioNumerator;
        }

        /// <summary>
        /// Gets the Pixel Ratio Denominator.
        /// </summary>
        public int PixelAspectRatioDenominator { get; private set; }

        /// <summary>
        /// Sets the PixelAspectRatioDenominator property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="intPixelAspectRatioDenominator">The Pixel Aspect Ratio Denominator value read from the file.</param>
        protected internal void SetPixelAspectRatioDenominator(int intPixelAspectRatioDenominator)
        {
            PixelAspectRatioDenominator = intPixelAspectRatioDenominator;
        }

        /// <summary>
        /// Gets the Pixel Aspect Ratio.
        /// </summary>
        public float PixelAspectRatio
        {
            get
            {
                if (PixelAspectRatioDenominator > 0)
                {
                    return PixelAspectRatioNumerator / (float)PixelAspectRatioDenominator;
                }

                return 0.0F;
            }
        }

        /// <summary>
        /// Gets the Gamma Numerator.
        /// </summary>
        public int GammaNumerator { get; private set; }

        /// <summary>
        /// Sets the GammaNumerator property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="intGammaNumerator">The Gamma Numerator value read from the file.</param>
        protected internal void SetGammaNumerator(int intGammaNumerator)
        {
            GammaNumerator = intGammaNumerator;
        }

        /// <summary>
        /// Gets the Gamma Denominator.
        /// </summary>
        public int GammaDenominator { get; private set; }

        /// <summary>
        /// Sets the GammaDenominator property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="intGammaDenominator">The Gamma Denominator value read from the file.</param>
        protected internal void SetGammaDenominator(int intGammaDenominator)
        {
            GammaDenominator = intGammaDenominator;
        }

        /// <summary>
        /// Gets the Gamma Ratio.
        /// </summary>
        public float GammaRatio
        {
            get
            {
                if (GammaDenominator > 0)
                {
                    var ratio = GammaNumerator / (float)GammaDenominator;
                    return (float)Math.Round(ratio, 1);
                }

                return 1.0F;
            }
        }

        /// <summary>
        /// Gets the offset from the beginning of the file to the start of the Color Correction table.
        /// </summary>
        public int ColorCorrectionOffset { get; private set; }

        /// <summary>
        /// Sets the ColorCorrectionOffset property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="intColorCorrectionOffset">The Color Correction Offset value read from the file.</param>
        protected internal void SetColorCorrectionOffset(int intColorCorrectionOffset)
        {
            ColorCorrectionOffset = intColorCorrectionOffset;
        }

        /// <summary>
        /// Gets the offset from the beginning of the file to the start of the Postage Stamp image data.
        /// </summary>
        public int PostageStampOffset { get; private set; }

        /// <summary>
        /// Sets the PostageStampOffset property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="intPostageStampOffset">The Postage Stamp Offset value read from the file.</param>
        protected internal void SetPostageStampOffset(int intPostageStampOffset)
        {
            PostageStampOffset = intPostageStampOffset;
        }

        /// <summary>
        /// Gets the offset from the beginning of the file to the start of the Scan Line table.
        /// </summary>
        public int ScanLineOffset { get; private set; }

        /// <summary>
        /// Sets the ScanLineOffset property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="intScanLineOffset">The Scan Line Offset value read from the file.</param>
        protected internal void SetScanLineOffset(int intScanLineOffset)
        {
            ScanLineOffset = intScanLineOffset;
        }

        /// <summary>
        /// Gets the type of Alpha channel data contained in the file.
        /// 0: No Alpha data included.
        /// 1: Undefined data in the Alpha field, can be ignored
        /// 2: Undefined data in the Alpha field, but should be retained
        /// 3: Useful Alpha channel data is present
        /// 4: Pre-multiplied Alpha (see description below)
        /// 5-127: RESERVED
        /// 128-255: Un-assigned
        /// </summary>
        public int AttributesType { get; private set; }

        /// <summary>
        /// Sets the AttributesType property, available only to objects in the same assembly as TargaExtensionArea.
        /// </summary>
        /// <param name="intAttributesType">The Attributes Type value read from the file.</param>
        protected internal void SetAttributesType(int intAttributesType)
        {
            AttributesType = intAttributesType;
        }

        /// <summary>
        /// Gets a list of offsets from the beginning of the file that point to the start of the next scan line, 
        /// in the order that the image was saved 
        /// </summary>
        public List<int> ScanLineTable { get; } = new List<int>();

        /// <summary>
        /// Gets a list of Colors where each Color value is the desired Color correction for that entry.
        /// This allows the user to store a correction table for image remapping or LUT driving.
        /// </summary>
        public List<Color> ColorCorrectionTable { get; } = new List<Color>();
    }


    /// <summary>
    /// Utilities functions used by the TargaImage class.
    /// </summary>
    static class Utilities
    {

        /// <summary>
        /// Gets an int value representing the subset of bits from a single Byte.
        /// </summary>
        /// <param name="b">The Byte used to get the subset of bits from.</param>
        /// <param name="offset">The offset of bits starting from the right.</param>
        /// <param name="count">The number of bits to read.</param>
        /// <returns>
        /// An int value representing the subset of bits.
        /// </returns>
        /// <remarks>
        /// Given -> b = 00110101 
        /// A call to GetBits(b, 2, 4)
        /// GetBits looks at the following bits in the byte -> 00{1101}00
        /// Returns 1101 as an int (13)
        /// </remarks>
        internal static int GetBits(byte b, int offset, int count)
        {
            return (b >> offset) & ((1 << count) - 1);
        }

        /// <summary>
        /// Reads ARGB values from the 16 bits of two given Bytes in a 1555 format.
        /// </summary>
        /// <param name="one">The first Byte.</param>
        /// <param name="two">The Second Byte.</param>
        /// <returns>A System.Drawing.Color with a ARGB values read from the two given Bytes</returns>
        /// <remarks>
        /// Gets the ARGB values from the 16 bits in the two bytes based on the below diagram
        /// |   BYTE 1   |  BYTE 2   |
        /// | A RRRRR GG | GGG BBBBB |
        /// </remarks>
        internal static Color GetColorFrom2Bytes(byte one, byte two)
        {
            // get the 5 bits used for the RED value from the first byte
            var r1 = GetBits(one, 2, 5);
            var r = r1 << 3;

            // get the two high order bits for GREEN from the from the first byte
            var bit = GetBits(one, 0, 2);
            // shift bits to the high order
            var g1 = bit << 6;

            // get the 3 low order bits for GREEN from the from the second byte
            bit = GetBits(two, 5, 3);
            // shift the low order bits
            var g2 = bit << 3;
            // add the shifted values together to get the full GREEN value
            var g = g1 + g2;

            // get the 5 bits used for the BLUE value from the second byte
            var b1 = GetBits(two, 0, 5);
            var b = b1 << 3;

            // get the 1 bit used for the ALPHA value from the first byte
            var a1 = GetBits(one, 7, 1);
            var a = a1 * 255;

            // return the resulting Color
            return Color.FromArgb(a, r, g, b);
        }

        /// <summary>
        /// Gets a 32 character binary string of the specified Int32 value.
        /// </summary>
        /// <param name="n">The value to get a binary string for.</param>
        /// <returns>A string with the resulting binary for the supplied value.</returns>
        /// <remarks>
        /// This method was used during debugging and is left here just for fun.
        /// </remarks>
        internal static string GetIntBinaryString(Int32 n)
        {
            var b = new char[32];
            var pos = 31;
            var i = 0;

            while (i < 32)
            {
                if ((n & (1 << i)) != 0)
                {
                    b[pos] = '1';
                }
                else
                {
                    b[pos] = '0';
                }
                pos--;
                i++;
            }
            return new string(b);
        }

        /// <summary>
        /// Gets a 16 character binary string of the specified Int16 value.
        /// </summary>
        /// <param name="n">The value to get a binary string for.</param>
        /// <returns>A string with the resulting binary for the supplied value.</returns>
        /// <remarks>
        /// This method was used during debugging and is left here just for fun.
        /// </remarks>
        internal static string GetInt16BinaryString(Int16 n)
        {
            var b = new char[16];
            var pos = 15;
            var i = 0;

            while (i < 16)
            {
                if ((n & (1 << i)) != 0)
                {
                    b[pos] = '1';
                }
                else
                {
                    b[pos] = '0';
                }
                pos--;
                i++;
            }
            return new string(b);
        }

    }
}
