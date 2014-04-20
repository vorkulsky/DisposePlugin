using System;
using System.Collections.Generic;
using System.IO;
using DisposePlugin.Services;
using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Cache
{
    public class DisposeMethodStatus
    {
        private readonly IPsiSourceFile _psiSourceFile;
        private readonly string _name;
        private readonly int _offset;
        private readonly IList<MethodArgumentStatus> _methodArguments;

        public DisposeMethodStatus(string name, int offset, IList<MethodArgumentStatus> methodArguments, IPsiSourceFile psiSourceFile)
        {
            _name = name;
            _offset = offset;
            _methodArguments = methodArguments;
            _psiSourceFile = psiSourceFile;
        }

        #region DisposeMethodStatus Members
        public IPsiSourceFile PsiSourceFile
        {
            get { return _psiSourceFile; }
        }

        public string Name
        {
            get { return _name; }
        }

        public int Offset
        {
            get { return _offset; }
        }

        public IList<MethodArgumentStatus> MethodArguments
        {
            get { return _methodArguments; }
        }
        #endregion DisposeMethodStatus Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Offset);
            writer.Write(MethodArguments.Count);
            foreach (var argument in MethodArguments)
                argument.Write(writer);
        }

        public static DisposeMethodStatus Read(BinaryReader reader, IPsiSourceFile psiSourceFile)
        {
            var name = reader.ReadString();
            var offset = reader.ReadInt32();
            var count = reader.ReadInt32();
            var methodArguments = new List<MethodArgumentStatus>(count);
            for (var i = 0; i < count; i++)
                methodArguments.Add(MethodArgumentStatus.Read(reader, psiSourceFile));
            return new DisposeMethodStatus(name, offset, methodArguments, psiSourceFile);
        }
    }

    public class MethodArgumentStatus
    {
        private readonly IPsiSourceFile _psiSourceFile;
        private readonly byte _number; // Для this равен 0
        private readonly VariableDisposeStatus _status;
        private readonly IList<InvokedExpressionData> _invokedExpressions;

        public MethodArgumentStatus(byte number, VariableDisposeStatus status, IList<InvokedExpressionData> invokedExpressions, IPsiSourceFile psiSourceFile)
        {
            _number = number;
            _status = status;
            _invokedExpressions = invokedExpressions;
            _psiSourceFile = psiSourceFile;
        }

        #region MethodArgumentStatus Members
        public IPsiSourceFile PsiSourceFile
        {
            get { return _psiSourceFile; }
        }

        public byte Number
        {
            get { return _number; }
        }

        public VariableDisposeStatus Status
        {
            get { return _status; }
        }

        public IList<InvokedExpressionData> InvokedExpressions
        {
            get { return _invokedExpressions; }
        }
        #endregion MethodArgumentStatus Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Number);
            writer.Write(Status.ToString());
            writer.Write(InvokedExpressions.Count);
            foreach (var expression in InvokedExpressions)
                expression.Write(writer);
        }

        public static MethodArgumentStatus Read(BinaryReader reader, IPsiSourceFile psiSourceFile)
        {
            var number = reader.ReadByte();
            var status = (VariableDisposeStatus) Enum.Parse(typeof(VariableDisposeStatus), reader.ReadString());
            var count = reader.ReadInt32();
            var invokedExpressions = new List<InvokedExpressionData>(count);
            for (var i = 0; i < count; i++)
                invokedExpressions.Add(InvokedExpressionData.Read(reader, psiSourceFile));
            return new MethodArgumentStatus(number, status, invokedExpressions, psiSourceFile);
        }
    }

    public class InvokedExpressionData
    {
        private readonly IPsiSourceFile _psiSourceFile;
        // Имя вызываемого метода
        private readonly string _name;
        // Offset выражения вызова
        private readonly int _offset;
        // Номер аргумента в выражении вызова метода
        // Для объекта, на котором вызывают, равен 0
        private readonly byte _argumentPosition;

        public InvokedExpressionData(string name, int offset, byte argumentPosition, IPsiSourceFile psiSourceFile)
        {
            _name = name;
            _offset = offset;
            _argumentPosition = argumentPosition;
            _psiSourceFile = psiSourceFile;
        }

        #region InvokedExpressionData Members
        public IPsiSourceFile PsiSourceFile
        {
            get { return _psiSourceFile; }
        }
        public string Name
        {
            get { return _name; }
        }

        public int Offset
        {
            get { return _offset; }
        }

        public byte ArgumentPosition
        {
            get { return _argumentPosition; }
        }
        #endregion InvokedExpressionData Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Offset);
            writer.Write(ArgumentPosition);
        }

        public static InvokedExpressionData Read(BinaryReader reader, IPsiSourceFile psiSourceFile)
        {
            var name = reader.ReadString();
            var offset = reader.ReadInt32();
            var argumentPosition = reader.ReadByte();
            return new InvokedExpressionData(name, offset, argumentPosition, psiSourceFile);
        }

        [CanBeNull]
        public static T GetNodeByOffset<T>([NotNull] IPsiSourceFile sourceFile, int offset) where T : class, ITreeNode
        {
            var file = sourceFile.GetPsiFile<CSharpLanguage>(new DocumentRange(sourceFile.Document, 0));
            if (file == null)
                return null;
            var elem = file.FindNodeAt(new TreeTextRange(new TreeOffset(offset), 1));
            if (elem == null)
                return null;
            var typedNode = GoUpToNodeWithType<T>(elem);
            return typedNode;
        }

        public static int GetOffsetByNode([NotNull] ITreeNode node)
        {
            var offset = node.GetNavigationRange().TextRange.StartOffset;
            return offset;
        }

        [CanBeNull]
        private static T GoUpToNodeWithType<T>([NotNull] ITreeNode node) where T : class, ITreeNode
        {
            while (true)
            {
                var typedNode = node as T;
                if (typedNode != null)
                    return typedNode;
                var parent = node.Parent;
                if (parent == null)
                    return null;
                node = parent;
            }
        }
    }
}
