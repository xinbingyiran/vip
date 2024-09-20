!function () {




    //////////////////
    //              //
    //   Overhead   //
    //              //
    //////////////////


    //  Create the comment node and a refernce to it
    //  then insert it above absolutely everything in the DOM.

    const
        frameWidth = 40,//  Default animation frame width in characters.
        frameHeight = 10,//  Default animation frame height in characters.


        //  Because Firefox and Safari do not support line returns in comment nodes,
        //  (I know, WTF, right?!)
        //  We have to create a separate comment node per line.

        commentNodes = new Array(frameHeight)
            .fill(1)
            .map(function () {

                const commentNode = document.createComment('')
                document.insertBefore(commentNode, document.firstChild)
                return commentNode
            })
            .reverse(),
        lineReturn = '\r\n',//  https://stackoverflow.com/questions/6539801/reminder-r-n-or-n-r


        //  Actual non-breaking space character,
        //  because Firefox and Safari will collapse regular whitespaces!

        charSpace = '\xa0'


    //  It’s probably nice to have a standard way of formatting
    //  the animation -- width, height, whitespace, and such.

    function render(a) {


        //  We’d like ‘a’ to be an Array
        //  but if it’s a String we’ll let it slide.

        if (typeof a === 'string') {

            if (a.indexOf(lineReturn) > -1) a = a.split(lineReturn)
            else a = [a]
        }


        //  Correct the height. Is it too short? Too tall?

        while (a.length < frameHeight) {a.push('')}
        if (a.length > frameHeight + 1) a = a.slice(0, frameHeight)


        //  Correct the width of each line by padding it to the intended width,
        //  then trimming it if it’s too long.
        //  Update the corresponding comment node.

        a.forEach(function (line, i) {

            commentNodes[i].nodeValue = line
                .replace(/ /g, charSpace)// Swap out regular spaces for non-breaking for Firefox and Safari.
                .padEnd(frameWidth, charSpace)
                .substr(0, frameWidth)
        })
    }




    //////////////////////
    //                  //
    //   Custom Logic   //
    //                  //
    //////////////////////


    const
        fish = {

            x: 0,//  Location
            v: 1,//  Vector (direction)
            w: 5,//  Sprite width
            s: 0,//  Sprite index
            sprites: [

                '}(°≤',
                '(°o°',
                '°o°)',
                '°o°)',
                'o°){',
                '≥°){'
            ],
            rendered: ''
        },
        bubbles = ['', '', '', ''],
        seaFloor = '__Vv_____________________________wW_v__V'

    let
        marquee = 'little fish big fish swimming in the water come back here man gimme my daughter',
        m = 0

    marquee = marquee.padStart(frameWidth * 2 + marquee.length, charSpace)
    function update() {


        //  Fish swims from LEFT to RIGHT.

        if (fish.v > 0) {

            if (fish.x < frameWidth) fish.x++
            if (fish.x + fish.w > frameWidth) {

                fish.s++
                if (fish.s === fish.sprites.length - 1) fish.v = -1
            }
        }


        //  Fish swims from RIGHT to LEFT.

        else {

            if (fish.x > 0) fish.x--
            if (fish.x - fish.w < fish.sprites.length - 1) {

                fish.s--
                if (fish.s === 0) fish.v = 1
            }
        }


        //  Pre-render the fish.

        fish.rendered = fish.sprites[fish.s].padStart(fish.x, charSpace)


        //  Process existing bubbles and float them up one line.

        bubbles[3] = bubbles[2]
        bubbles[2] = bubbles[1]
        bubbles[1] = bubbles[0]


        //  Our fish occasionally blows new bubbles.

        bubbles[0] = ''.padEnd(frameWidth, charSpace)
        bubbles.insertAt = -1
        if (Math.random() * 7 < 1) {

            if (fish.v > 0) bubbles.insertAt = fish.x
            else bubbles.insertAt = fish.x - fish.w
            bubbles[0] = bubbles[0].substr(0, bubbles.insertAt)
                + '°'
                + bubbles[0].substr(bubbles.insertAt + 1)
        }


        //  Scroll that marquee!

        m = (m + 1) % marquee.length


        //  Put it all together now.

        render([

            bubbles[3],
            bubbles[2],
            bubbles[1],
            bubbles[0],
            fish.rendered,
            '',
            '',
            seaFloor,
            '',
            marquee.substring(m, m + frameWidth)
        ])
    }

    //  Kick off the loopage.

    update()
    setInterval(update, 100)
}();