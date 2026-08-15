using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameEditor.ImageToMesh
{
    public sealed class ImageMeshRegionConfig : ScriptableObject
    {
        #region fields
        [SerializeField]
        private Mesh _mesh;
        [SerializeField]
        private List<Region> _regionList = new List<Region>();
        #endregion

        #region properties
        public Mesh Mesh => _mesh;
        public IReadOnlyList<Region> RegionList => _regionList;
        #endregion

        #region methods
        public void SetData(Mesh targetMesh, List<Region> targetRegions)
        {
            _mesh = targetMesh;
            _regionList = targetRegions;
        }

        public Region FindRegion(string regionName)
        {
            for (int i = 0; i < _regionList.Count; i++)
            {
                if (_regionList[i].Name == regionName)
                {
                    return _regionList[i];
                }
            }

            return null;
        }
        #endregion

        #region nested types
        [Serializable]
        public sealed class Region
        {
            #region fields
            [SerializeField]
            private string _regionName;
            [SerializeField]
            private int[] _vertexIndices;
            #endregion
            
            #region properties
            public string Name => _regionName;
            public IReadOnlyList<int> VertexIndices => _vertexIndices;
            #endregion
            
            #region methods
            public Region(string name, int[] indices)
            {
                _regionName = name;
                _vertexIndices = indices;
            }
            #endregion
        }
        #endregion
    }
}
