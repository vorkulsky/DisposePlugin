using System;
using System.Collections.Generic;
using System.IO;
using DisposePlugin.Services;
using JetBrains.ReSharper.Psi;

namespace DisposePlugin.Cache
{
    public class DisposeMethodStatus
    {
        private readonly IPsiSourceFile myPsiSourceFile;
        private string myName;
        private int myOffset;
        private IList<MethodArgumentStatus> myMethodArguments;

        public DisposeMethodStatus(string name, int offset, IList<MethodArgumentStatus> methodArguments, IPsiSourceFile psiSourceFile)
        {
            myName = name;
            myOffset = offset;
            myMethodArguments = methodArguments;
            myPsiSourceFile = psiSourceFile;
        }

        #region DisposeMethodStatus Members
        public IPsiSourceFile PsiSourceFile
        {
            get { return myPsiSourceFile; }
        }

        public string Name
        {
            get { return myName; }
        }

        public int Offset
        {
            get { return myOffset; }
        }

        public IList<MethodArgumentStatus> MethodArguments
        {
            get { return myMethodArguments; }
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
                methodArguments.Add(MethodArgumentStatus.Read(reader));
            return new DisposeMethodStatus(name, offset, methodArguments, psiSourceFile);
        }
    }

    public class MethodArgumentStatus
    {
        private byte myNumber; // Для this равен 0
        private VariableDisposeStatus myStatus;
        private IList<InvokedMethod> myInvokedMethods;

        public MethodArgumentStatus(byte number, VariableDisposeStatus status, IList<InvokedMethod> invokedMethods)
        {
            myNumber = number;
            myStatus = status;
            myInvokedMethods = invokedMethods;
        }

        #region MethodArgumentStatus Members
        public byte Number
        {
            get { return myNumber; }
        }

        public VariableDisposeStatus Status
        {
            get { return myStatus; }
        }

        public IList<InvokedMethod> InvokedMethods
        {
            get { return myInvokedMethods; }
        }
        #endregion MethodArgumentStatus Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Number);
            writer.Write(Status.ToString());
            writer.Write(InvokedMethods.Count);
            foreach (var method in InvokedMethods)
                method.Write(writer);
        }

        public static MethodArgumentStatus Read(BinaryReader reader)
        {
            var number = reader.ReadByte();
            var status = (VariableDisposeStatus) Enum.Parse(typeof(VariableDisposeStatus), reader.ReadString());
            var count = reader.ReadInt32();
            var invokedMethods = new List<InvokedMethod>(count);
            for (var i = 0; i < count; i++)
                invokedMethods.Add(InvokedMethod.Read(reader));
            return new MethodArgumentStatus(number, status, invokedMethods);
        }
    }

    public class InvokedMethod
    {
        private string myName;
        private int myOffset;
        private byte myArgumentPosition; // Для объекта, на котором вызывают, равен 0

        public InvokedMethod(string name, int offset, byte argumentPosition)
        {
            myName = name;
            myOffset = offset;
            myArgumentPosition = argumentPosition;
        }

        #region InvokedMethod Members
        public string Name
        {
            get { return myName; }
        }

        public int Offset
        {
            get { return myOffset; }
        }

        public byte ArgumentPosition
        {
            get { return myArgumentPosition; }
        }
        #endregion InvokedMethod Members

        public void Write(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Offset);
            writer.Write(ArgumentPosition);
        }

        public static InvokedMethod Read(BinaryReader reader)
        {
            var name = reader.ReadString();
            var offset = reader.ReadInt32();
            var argumentPosition = reader.ReadByte();
            return new InvokedMethod(name, offset, argumentPosition);
        }
    }
}
