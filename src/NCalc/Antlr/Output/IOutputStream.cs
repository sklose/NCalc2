// OsmSharp - OpenStreetMap tools & library.
// Copyright (C) 2012 Abelshausen Ben
// 
// This file is part of OsmSharp.
// 
// OsmSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// OsmSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with OsmSharp. If not, see <http://www.gnu.org/licenses/>.

namespace Antlr3.Runtime.PCL.Output
{
    /// <summary>
    /// Interface representing a listener that can be used to listen to output.
    /// </summary>
    public interface IOutputStream
    {
        /// <summary>
        /// Writes a line to the output stream.
        /// </summary>
        void WriteLine();

        /// <summary>
        /// Writes a line of text to the output stream.
        /// </summary>
        /// <param name="text"></param>
        void WriteLine(string text);

        /// <summary>
        /// Writes a line of text to the output stream.
        /// </summary>
        /// <param name="someObject"></param>
        void WriteLine(object someObject);

        /// <summary>
        /// Writes text to the output stream.
        /// </summary>
        /// <param name="text"></param>
        void Write(string text);

        /// <summary>
        /// Reports progress to the output stream.
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="key"></param>
        /// <param name="message"></param>
        void ReportProgress(double progress, string key, string message);
    }
}