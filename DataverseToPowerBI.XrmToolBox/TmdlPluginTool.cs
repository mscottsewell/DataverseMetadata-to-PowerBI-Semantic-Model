using System;
using System.ComponentModel.Composition;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace DataverseToPowerBI.XrmToolBox
{
    /// <summary>
    /// XrmToolBox plugin metadata and factory
    /// </summary>
    [Export(typeof(IXrmToolBoxPlugin)),
     ExportMetadata("Name", "Dataverse to Power BI Semantic Model"),
     ExportMetadata("Description", "Extract Dataverse metadata and generate Power BI TMDL/PBIP semantic models"),
     ExportMetadata("SmallImageBase64", "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAALEwAACxMBAJqcGAAAAVlJREFUWIXtl0EKwjAQRV9FvIobN+4Ed+5cuHDtUvAI3kHw/O5duHLjRnDhRnCl1VqaxNQ0tWjnQ0NI/p+ZpJlA4WcJgBG2Z4W8AewD9C2QG6AA6EfKe3B7VAJsjLGpABjCW0XKe/DVrgFgLIBwAJwhGALDSPn33FKQ/AQJYCxAMA6A/wAAiCn+wgAAAABJRU5ErkJggg=="),
     ExportMetadata("BigImageBase64", "iVBORw0KGgoAAAANSUhEUgAAAFAAAABQCAYAAACOEfKtAAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAALEwAACxMBAJqcGAAAAhxJREFUeJzt2rFuE0EQBuB/c86JPPUVCkoSKHgDGl6AhhegoqGioKKioaChoaChoqKioqKgoaGioaGh4A2oKegoUlJx5rIZ5RJhiX07t+z87vcMsXd2dr6bm93RFQD4YQIQAH4YgJ9IwA9zUDQgJqIYoKg5UHRFgLJe4HdE0aA8IEQe0F/OfxD0u/Ov+kCPxEF+iPIWyHLmJwf0CPQIxB8A7Ev8AIGcKP5EQJ+AAdAPAPgJGnyixwZwn/AbgIFQL1AMBf4C/wdYFf4AoI+g4j/4M4D1mP8z0CdwIsCcKHwJNwf0SPQ+g/oE8oR6Av4GsB7zHwT6BN4GJI+g4jcY7AP4g8BKU/8B6BNIA6PkO8gT6gVkCfQI5AnND/wJeALQk+gR6H0G9QnkCfQI9D6DPIEegR6B3mcwJ9Aj0PsM8gQk44f4C/wfYFf4AoI+g4qJoHLIH0DIC/wJQC+RQJ9A7zPoE6hPoE8gT+jfAPZj/oNAn0CPwF/Ov4He59P/BtoE0MAoEedGeCTKfxLoEegTyBNIEx2SPIG/BXoCOQJ9AmhglHwHFUPBn0DvM6hPoE+gJ/AXAA0M+j/QP+AnyMr4TQCdQBqY+g8O/wLQ5+r/H0CPQCYQMuqvAl4HCRoBPoH2p2B9P0Gg6yHYJoAGoI9g+gS6HoI+gR6BPoH+A/4HsB7zPwJ9AhXn/8/An4BkAAfxB4A+g4oT/8ehfwEBEW0Fxo8AAAAASUVORK5CYII="),
     ExportMetadata("BackgroundColor", "#2C5697"),
     ExportMetadata("PrimaryFontColor", "#FFFFFF"),
     ExportMetadata("SecondaryFontColor", "#CCCCCC")]
    public class TmdlPluginTool : PluginBase
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new PluginControl();
        }
    }
}
