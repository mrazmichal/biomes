// Terrain surface shader that blends different biome textures of neighboring cells together
// author: Michal Mráz
// based on "Godot - Rimworld style tilemap shader tutorial" by Vegard Beider, 
// source: https://www.youtube.com/watch?v=91fkApi8RUQ

Shader "Surface/terrainSurfaceShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SecondTex ("Texture", 2D) = "white" {}
        _ThirdTex ("Texture", 2D) = "white" {}
        _FourthTex ("Texture", 2D) = "white" {}
        _FifthTex ("BiomeIds", 2D) = "white" {}
        
        _WidthCellsCount("Float", Float) = 0.0
        _HeightCellsCount("Float", Float) = 0.0
        _ExtendedWidthCellsCount("Float", Float) = 0.0
        _ExtendedHeightCellsCount("Float", Float) = 0.0
        _WidthRatio("Float", Float) = 0.0
        _HeightRatio("Float", Float) = 0.0
        _CellWidthInUv("Float", Float) = 0.0
        _CellHeightInUv("Float", Float) = 0.0
        
        _SpecColor("Specular Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        CGPROGRAM
        #pragma surface surf BlinnPhong

        sampler2D _MainTex;
        sampler2D _SecondTex;
        sampler2D _ThirdTex;
        sampler2D _FourthTex;
        sampler2D _FifthTex;
        
        static const bool BLEND = true;

        static const float2 HERE = float2(0.0, 0.0);
        
        static const float2 NORTH = float2(0.0, 1.0);
        static const float2 EAST = float2(1.0, 0.0);
        static const float2 SOUTH = float2(0.0, -1.0);
        static const float2 WEST = float2(-1.0, 0.0);
        
        static const float2 NORTH_EAST = NORTH + EAST;
        static const float2 SOUTH_EAST = SOUTH + EAST;
        static const float2 SOUTH_WEST = SOUTH + WEST;
        static const float2 NORTH_WEST = NORTH + WEST;
        
        static const float2 NORTH_NORTH = NORTH + NORTH;
        static const float2 EAST_EAST = EAST + EAST;
        static const float2 SOUTH_SOUTH = SOUTH + SOUTH;
        static const float2 WEST_WEST = WEST + WEST;
        
        static const float2 NORTH_NORTH_EAST = NORTH_NORTH + EAST;
        static const float2 EAST_EAST_NORTH = EAST_EAST + NORTH;
        static const float2 EAST_EAST_SOUTH = EAST_EAST + SOUTH;
        static const float2 SOUTH_SOUTH_EAST = SOUTH_SOUTH + EAST;
        static const float2 SOUTH_SOUTH_WEST = SOUTH_SOUTH + WEST;
        static const float2 WEST_WEST_SOUTH = WEST_WEST + SOUTH;
        static const float2 WEST_WEST_NORTH = WEST_WEST + NORTH;
        static const float2 NORTH_NORTH_WEST = NORTH_NORTH + WEST;

        // Directions to neighbor cells. All of these influence biome blending in the current cell.
        static const int dirCount = 21;
        static const float2 directions[] = {
            HERE,
            NORTH, NORTH_EAST, EAST, SOUTH_EAST, SOUTH, SOUTH_WEST, WEST, NORTH_WEST,
            NORTH_NORTH, NORTH_NORTH_EAST,
            EAST_EAST_NORTH, EAST_EAST, EAST_EAST_SOUTH,
            SOUTH_SOUTH_EAST, SOUTH_SOUTH, SOUTH_SOUTH_WEST,
            WEST_WEST_SOUTH, WEST_WEST, WEST_WEST_NORTH,
            NORTH_NORTH_WEST    
        };
        
        uniform float _WidthCellsCount;
        uniform float _HeightCellsCount;            
        uniform float _ExtendedWidthCellsCount;
        uniform float _ExtendedHeightCellsCount;
        uniform float _WidthRatio;
        uniform float _HeightRatio;
        uniform float _CellWidthInUv;
        uniform float _CellHeightInUv;

        struct Input
        {
            float2 uv_MainTex;
        };

        fixed4 getBiomeColorAtUV(float2 cellsUV, float2 inputUV)
        {
            // texture tiling effect
            inputUV.x *= 8.0; // scale the texture differently in x and y because the chunk isn't square
            inputUV.y *= 6.0;

            // extract biomeId from texture
            fixed4 col = tex2D(_FifthTex, cellsUV);                
            int biomeId = int(floor((col.r * 255.0f) + 0.5f));

            // return a sample of texture of the appropriate biome
            fixed4 resCol = fixed4(0.0, 1.0, 0.0, 0.0);                
            if(biomeId == 0)
            {
                resCol = tex2D(_MainTex, inputUV);
            } else if(biomeId == 1)
            {
                resCol = tex2D(_SecondTex, inputUV);
            } else if(biomeId == 2)
            {
                resCol = tex2D(_ThirdTex, inputUV);
            } else if(biomeId == 3)
            {
                resCol = tex2D(_FourthTex, inputUV);
            }
            return resCol;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            // Settings for specular light
            o.Specular = 0.138125;     
            o.Gloss = 0.1;

            // inputUV goes from 0 to 1 within chunk borders 
            const float2 inputUV = IN.uv_MainTex;

            // But we assume two more cells behind chunk borders in every direction
            // we stretch the inputUV so that it includes also the extra cells and save it as uv
            float2 uv = inputUV;            
            uv -= float2(0.5, 0.5); // shift it so that we can stretch it around 0,0
            uv *= float2(_WidthRatio, _HeightRatio);
            uv += float2(0.5, 0.5); // return it back             

            // Cells coordinate system
            // starts and ends outside of chunk borders
            // whole part of number represents cell id, fractional part of number represents coordinates within cell (0 to 1)
            float2 cellsCoords;
            cellsCoords.x = uv.x * _ExtendedWidthCellsCount;
            cellsCoords.y = uv.y * _ExtendedHeightCellsCount;

            // Get cell center in uv coordinates
            float2 cellId = floor(cellsCoords);
            float2 cellCornerInUv = cellId / float2(_ExtendedWidthCellsCount, _ExtendedHeightCellsCount);
            float2 cellCenterInUv = cellCornerInUv + float2(_CellWidthInUv/2, _CellHeightInUv/2);

            // Cell center in cells coordinates
            float2 cellCenterInCellsCoords = cellId + float2(0.5, 0.5);

            // Size of cell in uv coordinates
            float2 cellSizeInUv = float2(_CellWidthInUv, _CellHeightInUv);

            fixed4 resColor = fixed4(0.0, 0.0, 0.0, 0.0);

            if (BLEND)
            {
                // Get centers of neighbour cells in uv coordinates
                float2 neighboursInUv[dirCount];
                for (int i = 0; i < dirCount; i++)
                {
                    neighboursInUv[i] = cellCenterInUv + directions[i] * cellSizeInUv;
                }

                // Get neighbour colors
                // For every neighbor ask what biome is there and get sample of it's texture at position inputUV 
                fixed4 neighboursColors[dirCount];
                for (int i = 0; i < dirCount; i++)
                {
                    neighboursColors[i] = getBiomeColorAtUV(neighboursInUv[i], inputUV);
                }

                // Get influences of neighbours on current fragment
                float influences[dirCount];
                for (int i = 0; i < dirCount; i++)
                {
                    influences[i] = smoothstep(2.0, 0.0, distance(cellsCoords, cellCenterInCellsCoords + directions[i]));
                }

                // Sum the influences
                float influSum = 0.0;
                for (int i = 0; i < dirCount; i++)
                {
                    influSum += influences[i];
                }

                // Make the influences add up to 1
                // And blend the colors using the influences as blending factors
                for (int i = 0; i < dirCount; i++)
                {
                    influences[i] /= influSum;
                    resColor += neighboursColors[i] * influences[i];
                }
                
            } else
            {   
                resColor = getBiomeColorAtUV(cellCenterInUv, inputUV);    
            }

            o.Albedo = resColor.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
