using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DataverseToPowerBI.Configurator.Forms
{
    /// <summary>
    /// Generates simple icons for the ribbon toolbar buttons
    /// </summary>
    public static class RibbonIcons
    {
        private const int IconSize = 20;

        /// <summary>
        /// Folder icon for Change Working Folder
        /// </summary>
        public static Image FolderIcon
        {
            get
            {
                var bmp = new Bitmap(IconSize, IconSize);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    
                    // Folder body
                    using var brush = new SolidBrush(Color.FromArgb(255, 200, 100));
                    using var pen = new Pen(Color.FromArgb(180, 140, 60), 1);
                    
                    // Folder tab
                    g.FillRectangle(brush, 2, 4, 6, 3);
                    g.DrawRectangle(pen, 2, 4, 6, 3);
                    
                    // Folder main body
                    g.FillRectangle(brush, 2, 6, 16, 10);
                    g.DrawRectangle(pen, 2, 6, 16, 10);
                }
                return bmp;
            }
        }

        /// <summary>
        /// Open folder icon
        /// </summary>
        public static Image OpenFolderIcon
        {
            get
            {
                var bmp = new Bitmap(IconSize, IconSize);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    
                    using var brush = new SolidBrush(Color.FromArgb(255, 200, 100));
                    using var pen = new Pen(Color.FromArgb(180, 140, 60), 1);
                    using var arrowBrush = new SolidBrush(Color.FromArgb(60, 120, 200));
                    
                    // Folder body (open/angled)
                    var points = new Point[] {
                        new Point(2, 6),
                        new Point(8, 4),
                        new Point(18, 4),
                        new Point(18, 16),
                        new Point(2, 16)
                    };
                    g.FillPolygon(brush, points);
                    g.DrawPolygon(pen, points);
                    
                    // Arrow pointing up-right
                    g.FillEllipse(arrowBrush, 12, 8, 6, 6);
                }
                return bmp;
            }
        }

        /// <summary>
        /// Cloud/server icon for Environment
        /// </summary>
        public static Image CloudIcon
        {
            get
            {
                var bmp = new Bitmap(IconSize, IconSize);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    
                    using var brush = new SolidBrush(Color.FromArgb(100, 160, 220));
                    using var pen = new Pen(Color.FromArgb(60, 100, 160), 1);
                    
                    // Cloud shape
                    g.FillEllipse(brush, 2, 8, 8, 8);
                    g.FillEllipse(brush, 6, 4, 10, 10);
                    g.FillEllipse(brush, 12, 8, 6, 8);
                    g.FillRectangle(brush, 4, 12, 12, 4);
                }
                return bmp;
            }
        }

        /// <summary>
        /// Table/grid icon for Select Tables
        /// </summary>
        public static Image TableIcon
        {
            get
            {
                var bmp = new Bitmap(IconSize, IconSize);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.None;
                    g.Clear(Color.Transparent);
                    
                    using var headerBrush = new SolidBrush(Color.FromArgb(100, 180, 100));
                    using var cellBrush = new SolidBrush(Color.FromArgb(220, 220, 220));
                    using var pen = new Pen(Color.FromArgb(100, 100, 100), 1);
                    
                    // Header row
                    g.FillRectangle(headerBrush, 2, 3, 16, 4);
                    
                    // Data rows
                    g.FillRectangle(cellBrush, 2, 7, 16, 4);
                    g.FillRectangle(cellBrush, 2, 11, 16, 4);
                    
                    // Grid lines
                    g.DrawRectangle(pen, 2, 3, 16, 12);
                    g.DrawLine(pen, 2, 7, 18, 7);
                    g.DrawLine(pen, 2, 11, 18, 11);
                    g.DrawLine(pen, 8, 3, 8, 15);
                    g.DrawLine(pen, 13, 3, 13, 15);
                }
                return bmp;
            }
        }

        /// <summary>
        /// Build/gear icon for Build Semantic Model
        /// </summary>
        public static Image BuildIcon
        {
            get
            {
                var bmp = new Bitmap(IconSize, IconSize);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    
                    using var brush = new SolidBrush(Color.FromArgb(80, 130, 200));
                    using var pen = new Pen(Color.FromArgb(50, 90, 150), 1.5f);
                    
                    // Outer gear circle
                    g.FillEllipse(brush, 3, 3, 14, 14);
                    g.DrawEllipse(pen, 3, 3, 14, 14);
                    
                    // Inner circle (gear hole)
                    using var innerBrush = new SolidBrush(Color.White);
                    g.FillEllipse(innerBrush, 7, 7, 6, 6);
                    g.DrawEllipse(pen, 7, 7, 6, 6);
                    
                    // Gear teeth (simplified)
                    g.DrawLine(pen, 10, 1, 10, 4);
                    g.DrawLine(pen, 10, 16, 10, 19);
                    g.DrawLine(pen, 1, 10, 4, 10);
                    g.DrawLine(pen, 16, 10, 19, 10);
                }
                return bmp;
            }
        }

        /// <summary>
        /// Model/cube icon for Semantic Model dropdown
        /// </summary>
        public static Image ModelIcon
        {
            get
            {
                var bmp = new Bitmap(IconSize, IconSize);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    
                    using var topBrush = new SolidBrush(Color.FromArgb(150, 100, 180));
                    using var leftBrush = new SolidBrush(Color.FromArgb(120, 70, 150));
                    using var rightBrush = new SolidBrush(Color.FromArgb(170, 130, 200));
                    using var pen = new Pen(Color.FromArgb(80, 50, 110), 1);
                    
                    // 3D cube
                    // Top face
                    var top = new Point[] {
                        new Point(10, 2),
                        new Point(18, 6),
                        new Point(10, 10),
                        new Point(2, 6)
                    };
                    g.FillPolygon(topBrush, top);
                    g.DrawPolygon(pen, top);
                    
                    // Left face
                    var left = new Point[] {
                        new Point(2, 6),
                        new Point(10, 10),
                        new Point(10, 18),
                        new Point(2, 14)
                    };
                    g.FillPolygon(leftBrush, left);
                    g.DrawPolygon(pen, left);
                    
                    // Right face
                    var right = new Point[] {
                        new Point(10, 10),
                        new Point(18, 6),
                        new Point(18, 14),
                        new Point(10, 18)
                    };
                    g.FillPolygon(rightBrush, right);
                    g.DrawPolygon(pen, right);
                }
                return bmp;
            }
        }

        /// <summary>
        /// Calendar icon for Calendar Table feature
        /// </summary>
        public static Image CalendarIcon
        {
            get
            {
                var bmp = new Bitmap(IconSize, IconSize);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.None;
                    g.Clear(Color.Transparent);

                    using var headerBrush = new SolidBrush(Color.FromArgb(200, 80, 80));
                    using var bodyBrush = new SolidBrush(Color.FromArgb(250, 250, 250));
                    using var dateBrush = new SolidBrush(Color.FromArgb(60, 60, 60));
                    using var pen = new Pen(Color.FromArgb(100, 100, 100), 1);

                    // Calendar body
                    g.FillRectangle(bodyBrush, 2, 4, 16, 13);
                    g.DrawRectangle(pen, 2, 4, 16, 13);

                    // Header (month bar) - red
                    g.FillRectangle(headerBrush, 2, 4, 16, 4);
                    g.DrawRectangle(pen, 2, 4, 16, 4);

                    // Calendar rings (top binding)
                    g.DrawLine(pen, 6, 2, 6, 5);
                    g.DrawLine(pen, 14, 2, 14, 5);

                    // Date grid (3x2 small squares)
                    for (int row = 0; row < 2; row++)
                    {
                        for (int col = 0; col < 3; col++)
                        {
                            g.FillRectangle(dateBrush, 4 + col * 5, 10 + row * 3, 3, 2);
                        }
                    }
                }
                return bmp;
            }
        }

        /// <summary>
        /// Refresh icon with circular arrow for Refresh Metadata
        /// </summary>
        public static Image RefreshIcon
        {
            get
            {
                var bmp = new Bitmap(IconSize, IconSize);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    
                    using var pen = new Pen(Color.FromArgb(60, 160, 60), 2f);
                    using var arrowBrush = new SolidBrush(Color.FromArgb(60, 160, 60));
                    
                    // Draw circular arrow (arc from 45 to 315 degrees)
                    g.DrawArc(pen, 3, 3, 14, 14, 45, 270);
                    
                    // Draw arrowhead at the end (pointing clockwise)
                    var arrowPoints = new Point[] {
                        new Point(16, 5),
                        new Point(18, 2),
                        new Point(18, 7)
                    };
                    g.FillPolygon(arrowBrush, arrowPoints);
                }
                return bmp;
            }
        }
    }
}
