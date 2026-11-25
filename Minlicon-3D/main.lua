-- mained.lua
function unminal.load()
    unminal.log("Lua Script Started!")
    local path = unminal.get_path("./cube.obj")

    cube = unminal.create_object(path)
    cube2 = unminal.create_object(path)
    
    if cube then
        unminal.set_position(cube, 0, 0, -10)
        unminal.set_scale(cube, 2, 2, 2)
        unminal.set_color(cube, 1, 1, 1, 1)
    else
        unminal.log("ERROR: Failed to create cube object!")
    end

    if cube2 then
        unminal.set_position(cube2, 0, 0, 0)
        unminal.set_scale(cube2, 2, 2, 2)
        unminal.set_color(cube2, 0, 1, 0, 1)
    else
        unminal.log("ERROR: Failed to create cube object!")
    end

    unminal.light.clear()
    unminal.light.add(2, 3, -8, 1, 1, 1, 5)

    unminal.command.register("lua_status", function()
        unminal.log("Lua scripting system is running!")
    end)

    unminal.command.register("set_position", function(x, y, z)
        if not x or not y or not z then
            unminal.log("Usage: set_position <x> <y> <z>")
            return
        end

        unminal.log("Position: " .. x .. ", " .. y .. ", " .. z)
    end)
end

function unminal.update(dt)
    -- Some Code Here
end

function unminal.draw()
    if cube then
        cube:Draw()
    end

    if cube2 then
        cube2:Draw()
    end
end

function unminal.unload()
    if cube then
        cube:Dispose()
    end
    unminal.light.clear()
    unminal.log("Lua Script Unloaded.")
end