#if UNITY_EDITOR
// One place to change the height of the house.
// After changing a number, run these menus again, in this order:
//   Haunted House > Build BIG House
//   Haunted House > Build 2ND FLOOR + ATTIC
//   Haunted House > Apply DETAILS (textures, trim, doors, frames)
// then select BigHouse > Nav Mesh Surface > Bake, and save the scene.
public static class HouseDims
{
    public const float H = 3.6f;             // ceiling height of the ground floor and the 2nd floor (was 3.2)
    public const float HA = 3.2f;            // ceiling height of the attic (was 3.0)

    public const float SLAB = 0.3f;          // thickness of floors / ceilings
    public const float F2 = H + SLAB;        // top of the 2nd floor
    public const float F3 = F2 + H + SLAB;   // top of the attic floor
    public const float BASEMENT_Y = -3.5f;   // basement floor

    // keep H at 3.8 or lower: above that the stairs get steps too tall to climb
}
#endif
