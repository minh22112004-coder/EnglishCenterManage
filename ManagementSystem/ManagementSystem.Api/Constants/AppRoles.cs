namespace ManagementSystem.Api.Constants
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string Lecture = "Lecture";
        public const string User = "User";

        public const string AdminOrLecture = "Admin,Lecture";
        public const string AdminOrUser = "Admin,User";
        public const string LectureOrUser = "Lecture,User";
        public const string All = "Admin,Lecture,User";
    }
}
