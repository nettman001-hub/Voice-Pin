using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VoicePin.App.Views;

public class AdminMemberItem
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Status { get; set; } = "활성";
    public string Info { get; set; } = "";
    public Brush StatusBrush => Status == "정지"
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDECEC"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E7F7EF"));
}

public partial class AdminMembersPage : Page
{
    private readonly ObservableCollection<AdminMemberItem> _members = new();

    public AdminMembersPage()
    {
        InitializeComponent();
        _members.Add(new AdminMemberItem { Name = "홍길동", Email = "hong@example.com", Info = "가입일 2026-03-01 · 프로 · 최근 판매 128건" });
        _members.Add(new AdminMemberItem { Name = "김영희", Email = "kim@example.com", Status = "정지", Info = "가입일 2026-02-15 · 베이직 · 신고 이력 1건" });
        _members.Add(new AdminMemberItem { Name = "이철수", Email = "lee@example.com", Info = "가입일 2026-05-20 · 프리미엄 · 최근 판매 42건" });

        MemberList.ItemsSource = _members;
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.Trim() ?? "";
        MemberList.ItemsSource = query.Length == 0
            ? _members
            : new ObservableCollection<AdminMemberItem>(
                _members.Where(m => m.Email.Contains(query, StringComparison.OrdinalIgnoreCase)
                                    || m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (MemberList.SelectedItem is not AdminMemberItem member)
        {
            MessageBox.Show("회원을 선택해 주세요.", "다들려");
            return;
        }
        if (member.Status == "정지")
        {
            member.Status = "활성";
            MessageBox.Show($"{member.Email} 회원의 정지를 해제했습니다. (데모)", "다들려");
        }
        else
        {
            member.Status = "정지";
            MessageBox.Show($"{member.Email} 회원을 정지했습니다. (데모)", "다들려");
        }
        MemberList.Items.Refresh();
    }
}
