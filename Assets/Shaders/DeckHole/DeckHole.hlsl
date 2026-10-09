// (+10/9) 수리 장판 자리 데크 구멍. DeckHole.cs가 전역 배열로 구멍 상자를 넘긴다.
// 상자 안 픽셀은 Alpha 0 → 알파 클립으로 안 그린다(그림자 · 깊이 패스도 같이 빠진다).
#ifndef MAREA_DECK_HOLE_INCLUDED
#define MAREA_DECK_HOLE_INCLUDED

#define DECK_HOLE_MAX 16
float4 _DeckHoleCenter[DECK_HOLE_MAX];   // xyz 중심(월드)
float4 _DeckHoleSize[DECK_HOLE_MAX];     // x 반폭(월드 X), y 반깊이(월드 Z), z 위아래 허용(데크 윗면만 뚫게)
float _DeckHoleCount;

void DeckHole_float(float3 WorldPos, out float Alpha)
{
    Alpha = 1;
    int count = (int)_DeckHoleCount;
    for (int i = 0; i < DECK_HOLE_MAX; i++)
    {
        if (i >= count) break;
        float3 d = abs(WorldPos - _DeckHoleCenter[i].xyz);
        if (d.x < _DeckHoleSize[i].x && d.z < _DeckHoleSize[i].y && d.y < _DeckHoleSize[i].z) Alpha = 0;
    }
}

void DeckHole_half(half3 WorldPos, out half Alpha)
{
    float a;
    DeckHole_float(WorldPos, a);
    Alpha = a;
}

#endif
