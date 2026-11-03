var r = new Random();
while (true)
{
    int min = 1, max = 100;
    int target = r.Next(min, max);
    int guessed;
    while (true)
    {
        while (true)
        {
            Console.WriteLine("--------------心冰依然猜数字游戏-----------------");
            Console.WriteLine($"请输入你要猜的数字[{min}-{max}]（回车结束）：");
            var numberStr = Console.ReadLine();
            Console.Clear();
            if (!int.TryParse(numberStr, out guessed))
            {
                Console.WriteLine("不正确的数字格式！");
                continue;
            }
            break;
        }
        //get guessed

        if (guessed == target)
        {
            Console.WriteLine("恭喜你答对了！");
            break;
        }
        else if (guessed < target)
        {
            if (guessed >= min)
            {
                min = guessed + 1;
            }
            Console.WriteLine("你猜小了，请重猜一次！");
        }
        else
        {
            if (guessed <= max)
            {
                max = guessed - 1;
            }
            Console.WriteLine("你猜大了，请重猜一次！");
        }
    }

    Console.WriteLine("是否继续玩？（1： 继续，其它： 退出）");
    if(Console.ReadLine() == "1")
    {
        continue;
    }
    break;
}