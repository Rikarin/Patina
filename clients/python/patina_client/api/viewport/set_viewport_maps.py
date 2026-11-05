from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.viewport_map_paths import ViewportMapPaths
from ...models.viewport_state import ViewportState
from typing import cast



def _get_kwargs(
    *,
    body: ViewportMapPaths,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "put",
        "url": "/api/v1/viewport/maps",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | ViewportState | None:
    if response.status_code == 200:
        response_200 = ViewportState.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails | ViewportState]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportMapPaths,

) -> Response[ProblemDetails | ViewportState]:
    """ Lays pictures over the mesh's own materials. An empty body takes them off.

    Args:
        body (ViewportMapPaths): Pictures to lay over the mesh's own materials, on every part.
            Each is a path on the Server's machine, and a slot
            left null keeps what the material asks for. A filled slot replaces the material's factor
            too, so what shows
            is the picture. Roughness is read from green, metallic from blue and occlusion from red,
            so one packed file or
            three grey ones both work. PNG, JPEG and EXR are read, base colour as sRGB and the rest as
            linear.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportState]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportMapPaths,

) -> ProblemDetails | ViewportState | None:
    """ Lays pictures over the mesh's own materials. An empty body takes them off.

    Args:
        body (ViewportMapPaths): Pictures to lay over the mesh's own materials, on every part.
            Each is a path on the Server's machine, and a slot
            left null keeps what the material asks for. A filled slot replaces the material's factor
            too, so what shows
            is the picture. Roughness is read from green, metallic from blue and occlusion from red,
            so one packed file or
            three grey ones both work. PNG, JPEG and EXR are read, base colour as sRGB and the rest as
            linear.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportState
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportMapPaths,

) -> Response[ProblemDetails | ViewportState]:
    """ Lays pictures over the mesh's own materials. An empty body takes them off.

    Args:
        body (ViewportMapPaths): Pictures to lay over the mesh's own materials, on every part.
            Each is a path on the Server's machine, and a slot
            left null keeps what the material asks for. A filled slot replaces the material's factor
            too, so what shows
            is the picture. Roughness is read from green, metallic from blue and occlusion from red,
            so one packed file or
            three grey ones both work. PNG, JPEG and EXR are read, base colour as sRGB and the rest as
            linear.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportState]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportMapPaths,

) -> ProblemDetails | ViewportState | None:
    """ Lays pictures over the mesh's own materials. An empty body takes them off.

    Args:
        body (ViewportMapPaths): Pictures to lay over the mesh's own materials, on every part.
            Each is a path on the Server's machine, and a slot
            left null keeps what the material asks for. A filled slot replaces the material's factor
            too, so what shows
            is the picture. Roughness is read from green, metallic from blue and occlusion from red,
            so one packed file or
            three grey ones both work. PNG, JPEG and EXR are read, base colour as sRGB and the rest as
            linear.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportState
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
