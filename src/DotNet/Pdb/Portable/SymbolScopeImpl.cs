// dnlib: See LICENSE.txt for more info

using System;
using System.Collections.Generic;
using System.Diagnostics;
using dnlib.DotNet.MD;
using dnlib.DotNet.Pdb.Symbols;

namespace dnlib.DotNet.Pdb.Portable {
    sealed class SymbolScopeImpl : SymbolScope {
        readonly PortablePdbReader owner;
        internal SymbolMethod? method;
        readonly SymbolScopeImpl? parent;
        readonly int startOffset;
        readonly int endOffset;
        internal readonly List<SymbolScope> childrenList;
        internal readonly List<SymbolVariable> localsList;
        internal PdbImportScope? importScope;
        readonly PdbCustomDebugInfo[] customDebugInfos;

        public override SymbolMethod Method {
            get {
                if (method is not null)
                    return method;
                var p = parent;
                Debug.Assert(p is not null);
                if (p is null)
                    return method ?? throw new InvalidOperationException();
                for (;;) {
                    if (p.parent is null)
                        return method = p.method!;
                    p = p.parent;
                }
            }
        }

        public override SymbolScope? Parent => parent;
        public override int StartOffset => startOffset;
        public override int EndOffset => endOffset;
        public override IList<SymbolScope> Children => childrenList;
        public override IList<SymbolVariable> Locals => localsList;
        public override IList<SymbolNamespace> Namespaces => Array2.Empty<SymbolNamespace>();
        public override IList<PdbCustomDebugInfo> CustomDebugInfos => customDebugInfos;
        public override PdbImportScope? ImportScope => importScope;

        public SymbolScopeImpl(PortablePdbReader owner, SymbolScopeImpl? parent, int startOffset, int endOffset, PdbCustomDebugInfo[] customDebugInfos) {
            this.owner = owner;
            method = null;
            this.parent = parent;
            this.startOffset = startOffset;
            this.endOffset = endOffset;
            childrenList = new List<SymbolScope>();
            localsList = new List<SymbolVariable>();
            this.customDebugInfos = customDebugInfos;
        }

        Metadata? constantsMetadata;
        RidList constantRidList;

        internal void SetConstants(Metadata metadata, RidList rids) {
            constantsMetadata = metadata;
            constantRidList = rids;
        }

        public override IList<PdbConstant> GetConstants(ModuleDef module, GenericParamContext gpContext) {
            if (constantRidList.Count == 0)
                return Array2.Empty<PdbConstant>();
            Debug.Assert(constantsMetadata is not null);

            var res = new PdbConstant[constantRidList.Count];
            int w = 0;
            for (int i = 0; i < res.Length; i++) {
                uint rid = constantRidList[i];
                bool b = constantsMetadata.TablesStream.TryReadLocalConstantRow(rid, out var row);
                Debug.Assert(b);
                var name = constantsMetadata.StringsStream.Read(row.Name);
                if (!constantsMetadata.BlobStream.TryCreateReader(row.Signature, out var reader))
                    continue;
                var localConstantSigBlobReader = new LocalConstantSigBlobReader(module, ref reader, gpContext);
                if (localConstantSigBlobReader.Read(out var type, out var value) && name is not null) {
                    var pdbConstant = new PdbConstant(name, type, value);
                    int token = new MDToken(Table.LocalConstant, rid).ToInt32();
                    owner.GetCustomDebugInfos(token, gpContext, pdbConstant.CustomDebugInfos);
                    res[w++] = pdbConstant;
                }
            }
            if (res.Length != w)
                Array.Resize(ref res, w);
            return res;
        }
    }
}
