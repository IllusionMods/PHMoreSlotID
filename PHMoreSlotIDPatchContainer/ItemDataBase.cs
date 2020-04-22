namespace PHMoreSlotIDPatchContainer
{
    public static class ItemDataBase
    {
        public static void CtorPostfix(ref int idField, int id)
        {
            idField = ((id > 999999 && id < 1000000000) ? id : (id % 1000));
        }
    }
}
